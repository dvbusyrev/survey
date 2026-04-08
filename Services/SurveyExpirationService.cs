using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MainProject.Models;
using Microsoft.Extensions.Configuration;

namespace MainProject.Services
{
    public class SurveyExpirationService : IHostedService, IDisposable
    {
        private readonly ILogger<SurveyExpirationService> _logger;
        private Timer? _timer;
        private readonly string _connectionString;

        public SurveyExpirationService(ILogger<SurveyExpirationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Фоновая служба обработки просрочки запущена");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromDays(1));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
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
                                IdSurvey = reader.GetInt32(0),
                                NameSurvey = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                DateCreate = reader.GetDateTime(3),
                                DateOpen = reader.GetDateTime(4),
                                DateClose = reader.GetDateTime(5)
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
                    BlockExpiredOrganization(organization);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при блокировке организации {OrganizationId}", organization.OrganizationId);
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
                                OrganizationId = reader.GetInt32(0),
                                OrganizationName = reader.GetString(1),
                                DateBegin = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                                DateEnd = reader.GetDateTime(3),
                                Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Block = reader.GetBoolean(5)
                            });
                        }
                    }
                }
            }

            return expiredOrganizations;
        }

        private void BlockExpiredOrganization(Organization organization)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var updateOrganizationQuery = @"UPDATE public.organization 
                                              SET block = true 
                                              WHERE organization_id = @IdOrganization";

                        using (var command = new NpgsqlCommand(updateOrganizationQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdOrganization", organization.OrganizationId);
                            int affectedRows = command.ExecuteNonQuery();

                            if (affectedRows == 0)
                            {
                                throw new Exception("Организация не найдена");
                            }
                        }

                        transaction.Commit();
                        _logger.LogInformation(
                            "Организация {OrganizationId} заблокирована из-за просрочки. Привязки пользователей к организации сохранены.",
                            organization.OrganizationId);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка блокировки организации {OrganizationId}", organization.OrganizationId);
                        throw;
                    }
                }
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
