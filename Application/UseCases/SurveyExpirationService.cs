using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MainProject.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace MainProject.Application.UseCases
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
                var today = DateTime.Today;

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

                var query = @"SELECT
                                  s.id_survey,
                                  s.name_survey,
                                  s.description,
                                  ss.date_begin,
                                  ss.date_end
                              FROM public.survey s
                              INNER JOIN public.survey_schedule ss
                                  ON ss.id_survey = s.id_survey
                              WHERE EXISTS (
                                      SELECT 1
                                      FROM public.organization_survey os
                                      WHERE os.id_survey = s.id_survey
                                  )
                                AND NOT EXISTS (
                                      SELECT 1
                                      FROM public.organization_survey os
                                      WHERE os.id_survey = s.id_survey
                                        AND os.date_end >= @CurrentDate::date
                                  )";

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
                                DateBegin = reader.GetDateTime(3),
                                DateEnd = reader.GetDateTime(4)
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
        }

        private List<Organization> GetExpiredOrganizations(DateTime currentDate)
        {
            var expiredOrganizations = new List<Organization>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"SELECT id_organization, organization_name, date_begin, date_end, email
                            FROM public.organization 
                            WHERE date_end < @CurrentDate::date";

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
                                Email = reader.IsDBNull(4) ? null : reader.GetString(4)
                            });
                        }
                    }
                }
            }

            return expiredOrganizations;
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
