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
                
                // Обработка OMSU
                ProcessExpiredOmsus(today);
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

            _logger.LogInformation("Найдено {Count} просроченных анкет", expiredSurveys.Count);

            foreach (var survey in expiredSurveys)
            {
                try
                {
                    ArchiveExpiredSurvey(survey, currentDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке анкеты {SurveyId}", survey.id_survey);
                }
            }
        }

        private List<Survey> GetExpiredSurveys(DateTime currentDate)
        {
            var expiredSurveys = new List<Survey>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"SELECT id_survey, name_survey, description, questions, 
                            date_create, date_open, date_close 
                            FROM surveys 
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
                                questions = reader.IsDBNull(3) ? null : reader.GetString(3),
                                date_create = reader.GetDateTime(4),
                                date_open = reader.GetDateTime(5),
                                date_close = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }

            return expiredSurveys;
        }

        private void ArchiveExpiredSurvey(Survey survey, DateTime overdueDate)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Перенос в архив
                        var insertQuery = @"
                            INSERT INTO history_surveys 
                                (date_begin, date_end, id_survey, file_questions, name_survey, description)
                            VALUES 
                                (@DateBegin, @DateEnd, @IdSurvey, @FileQuestions, @NameSurvey, @Description)";

                        using (var command = new NpgsqlCommand(insertQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@DateBegin", survey.date_open);
                            command.Parameters.AddWithValue("@DateEnd", survey.date_close);
                            command.Parameters.AddWithValue("@IdSurvey", survey.id_survey);
                            command.Parameters.AddWithValue("@NameSurvey", survey.name_survey);
                            command.Parameters.AddWithValue("@Description", survey.description ?? (object)DBNull.Value);
                            
                            var fileQuestionsParam = new NpgsqlParameter("@FileQuestions", NpgsqlTypes.NpgsqlDbType.Jsonb)
                            {
                                Value = string.IsNullOrEmpty(survey.questions) ? (object)DBNull.Value : survey.questions
                            };
                            command.Parameters.Add(fileQuestionsParam);
                            
                            command.ExecuteNonQuery();
                        }

                        // Удаление из основной таблицы
                        var deleteQuery = "DELETE FROM surveys WHERE id_survey = @Id";
                        using (var command = new NpgsqlCommand(deleteQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Id", survey.id_survey);
                            command.ExecuteNonQuery();
                        }

                        // Создаем лог
                        CreateLog(
                            connection: connection,
                            transaction: transaction,
                            idUser: 0,
                            idTarget: survey.id_survey,
                            targetType: "survey",
                            eventType: "SURVEY_OVERDUE",
                            description: "Срок прохождения анкеты истёк",
                            extraData: new JObject
                            {
                                ["survey_name"] = survey.name_survey,
                                ["original_end_date"] = survey.date_close
                            });

                        transaction.Commit();
                        _logger.LogInformation("Анкета {SurveyId} перенесена в архив", survey.id_survey);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка архивации анкеты {SurveyId}", survey.id_survey);
                        throw;
                    }
                }
            }
        }
        #endregion

        #region OMSU Processing
        private void ProcessExpiredOmsus(DateTime currentDate)
        {
            var expiredOmsus = GetExpiredOmsus(currentDate);

            _logger.LogInformation("Найдено {Count} просроченных OMSU", expiredOmsus.Count);

            foreach (var omsu in expiredOmsus)
            {
                try
                {
                    BlockExpiredOmsu(omsu, currentDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при блокировке OMSU {OmsuId}", omsu.id_omsu);
                }
            }
        }

        private List<OMSU> GetExpiredOmsus(DateTime currentDate)
        {
            var expiredOmsus = new List<OMSU>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"SELECT id_omsu, name_omsu, date_begin, date_end, email, block, list_surveys
                            FROM omsu 
                            WHERE date_end < @CurrentDate AND block = false";

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CurrentDate", currentDate);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            expiredOmsus.Add(new OMSU
                            {
                                id_omsu = reader.GetInt32(0),
                                name_omsu = reader.GetString(1),
                                date_begin = reader.GetDateTime(2),
                                date_end = reader.GetDateTime(3),
                                email = reader.GetString(4),
                                block = reader.GetBoolean(5),
                                list_surveys = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return expiredOmsus;
        }

        private void BlockExpiredOmsu(OMSU omsu, DateTime blockDate)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Блокировка OMSU
                        var updateOmsuQuery = @"UPDATE omsu 
                                              SET block = true 
                                              WHERE id_omsu = @IdOmsu";

                        using (var command = new NpgsqlCommand(updateOmsuQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdOmsu", omsu.id_omsu);
                            int affectedRows = command.ExecuteNonQuery();

                            if (affectedRows == 0)
                            {
                                throw new Exception("OMSU не найден");
                            }
                        }

                        // Очистка id_omsu у пользователей
                        var clearUserOmsuQuery = @"UPDATE users 
                                                 SET id_omsu = NULL 
                                                 WHERE id_omsu = @IdOmsu";
                        using (var command = new NpgsqlCommand(clearUserOmsuQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdOmsu", omsu.id_omsu);
                            command.ExecuteNonQuery();
                        }

                        // Создаем лог
                        CreateLog(
                            connection: connection,
                            transaction: transaction,
                            idUser: 0,
                            idTarget: omsu.id_omsu,
                            targetType: "organization",
                            eventType: "BLOCK_OMSU",
                            description: "Блокировка организации",
                            extraData: new JObject
                            {
                                ["organization_name"] = omsu.name_omsu,
                                ["email"] = omsu.email,
                                ["original_end_date"] = omsu.date_end
                            });

                        transaction.Commit();
                        _logger.LogInformation("OMSU {OmsuId} заблокирован и очищен у пользователей", omsu.id_omsu);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка блокировки OMSU {OmsuId}", omsu.id_omsu);
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
        INSERT INTO logs 
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