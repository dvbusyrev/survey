using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using main_project.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace main_project.Services
{
    public class SurveyExpirationService : IHostedService, IDisposable
    {
        private readonly ILogger<SurveyExpirationService> _logger;
        private Timer _timer;
        private readonly string _connectionString;

        public SurveyExpirationService(ILogger<SurveyExpirationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Фоновая служба обработки просрочки запущена");
            
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromDays(1));
            
            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            _logger.LogInformation("Начата обработка просроченных данных");
            
            try
            {
                var today = DateTime.Now;
                
                // Обработка анкет
                ProcessExpiredSurveys(today);
                
                // Обработка организаций
                ProcessExpiredOrganizations(today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в работе фоновой службы");
            }
        }

        #region Survey Processing
        private void ProcessExpiredSurveys(DateTime currentDate)
        {
            var expiredSurveys = GetExpiredSurveys(currentDate);

            _logger.LogInformation(
                "Найдено {Count} просроченных анкет. Перенос в архив отключен: анкеты остаются в public.survey и считаются архивными по дате закрытия.",
                expiredSurveys.Count);
        }

        private List<Survey> GetExpiredSurveys(DateTime currentDate)
        {
            var expiredSurveys = new List<Survey>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"SELECT id_survey, name_survey, description, date_create, date_open, date_close
                              FROM public.survey 
                              WHERE date_close < @CurrentDate";

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CurrentDate", currentDate);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            expiredSurveys.Add(new Survey
                            {
                                id_survey = reader.GetInt32(0),
                                name_survey = reader.GetString(1),
                                description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                date_create = reader.GetDateTime(3),
                                date_open = reader.GetDateTime(4),
                                date_close = reader.GetDateTime(5)
                            });
                        }
                    }
                }
            }

            return expiredSurveys;
        }

        #endregion

        #region Organization Processing
        private void ProcessExpiredOrganizations(DateTime currentDate)
        {
            var expiredOrganizations = GetExpiredOrganizations(currentDate);

            _logger.LogInformation("Найдено {Count} просроченных организаций", expiredOrganizations.Count);

            foreach (var organization in expiredOrganizations)
            {
                try
                {
                    BlockExpiredOrganization(organization, currentDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при блокировке организации {OrganizationId}", organization.organization_id);
                }
            }
        }

        private List<Organization> GetExpiredOrganizations(DateTime currentDate)
        {
            var expiredOrganizations = new List<Organization>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"SELECT organization_id, organization_name, date_begin, date_end, email, block
                            FROM public.organization 
                            WHERE date_end < @CurrentDate AND block = false";

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CurrentDate", currentDate);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            expiredOrganizations.Add(new Organization
                            {
                                organization_id = reader.GetInt32(0),
                                organization_name = reader.GetString(1),
                                date_begin = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                                date_end = reader.GetDateTime(3),
                                email = reader.IsDBNull(4) ? null : reader.GetString(4),
                                block = reader.GetBoolean(5)
                            });
                        }
                    }
                }
            }

            return expiredOrganizations;
        }

        private void BlockExpiredOrganization(Organization organization, DateTime blockDate)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Блокировка организации
                        var updateOrganizationQuery = @"UPDATE public.organization 
                                              SET block = true 
                                              WHERE organization_id = @IdOrganization";

                        using (var command = new NpgsqlCommand(updateOrganizationQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdOrganization", organization.organization_id);
                            int affectedRows = command.ExecuteNonQuery();

                            if (affectedRows == 0)
                            {
                                throw new Exception("Организация не найдена");
                            }
                        }

                        // Очистка organization_id у пользователей
                        var clearUserOrganizationQuery = @"UPDATE public.app_user 
                                                 SET organization_id = NULL 
                                                 WHERE organization_id = @IdOrganization";
                        using (var command = new NpgsqlCommand(clearUserOrganizationQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdOrganization", organization.organization_id);
                            command.ExecuteNonQuery();
                        }

                        // Создаем лог
                        CreateLog(
                            connection: connection,
                            transaction: transaction,
                            idUser: 0,
                            idTarget: organization.organization_id,
                            targetType: "organization",
                            eventType: "BLOCK_ORGANIZATION",
                            description: "Блокировка организации",
                            extraData: new JObject
                            {
                                ["organization_name"] = organization.organization_name,
                                ["email"] = organization.email,
                                ["original_end_date"] = organization.date_end
                            });

                        transaction.Commit();
                        _logger.LogInformation("Организация {OrganizationId} заблокирована и очищена у пользователей", organization.organization_id);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка блокировки организации {OrganizationId}", organization.organization_id);
                        throw;
                    }
                }
            }
        }
        #endregion

        #region Logging
       private void CreateLog(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    int idUser,
    int? idTarget,
    string targetType,
    string eventType,
    string description,
    JObject extraData = null)
{
    var query = @"
        INSERT INTO public.log 
            (id_user, id_target, target_type, event_type, date, description, extra_data)
        VALUES 
            (@IdUser, @IdTarget, @TargetType, @EventType, @Date, @Description, @ExtraData)";

    using (var command = new NpgsqlCommand(query, connection, transaction))
    {
        command.Parameters.AddWithValue("@IdUser", idUser);
        command.Parameters.AddWithValue("@IdTarget", idTarget ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TargetType", targetType);
        command.Parameters.AddWithValue("@EventType", eventType);
        command.Parameters.AddWithValue("@Date", DateTime.Now);
        command.Parameters.AddWithValue("@Description", description);
        
        // Явное указание типа параметра как jsonb
        var extraDataParam = new NpgsqlParameter("@ExtraData", NpgsqlTypes.NpgsqlDbType.Jsonb)
        {
            Value = extraData?.ToString(Newtonsoft.Json.Formatting.None) ?? (object)DBNull.Value
        };
        command.Parameters.Add(extraDataParam);
        
        command.ExecuteNonQuery();
    }
        }
        #endregion

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Фоновая служба обработки просрочки остановлена");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
