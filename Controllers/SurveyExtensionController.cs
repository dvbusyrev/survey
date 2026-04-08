using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Database;
using Npgsql;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainProject.Controllers
{
    public class SurveyExtensionController : Controller
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<SurveyExtensionController> _logger;

        public SurveyExtensionController(IDbConnectionFactory connectionFactory, ILogger<SurveyExtensionController> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        [HttpPost]
        [Route("survey-extensions")]
        [Route("survey_extensions")]
        public IActionResult SaveSurveyExtensions([FromBody] SurveyExtensionRequest request)
        {
            _logger.LogInformation("Получен запрос на продление анкеты: {Request}", JsonSerializer.Serialize(request));

            try
            {
                if (request == null || request.Extensions == null || request.Extensions.Count == 0)
                {
                    _logger.LogWarning("Пустой запрос на продление");
                    return BadRequest(new { success = false, message = "Необходимо предоставить данные для продления" });
                }

                var errors = ValidateRequest(request);
                if (errors.Count > 0)
                {
                    _logger.LogWarning("Ошибки валидации: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { 
                        success = false,
                        message = "Ошибки валидации",
                        errors = errors
                    });
                }

                using (var connection = _connectionFactory.CreateConnection())
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            ProcessExtensions(request, connection, transaction);
                            
                            transaction.Commit();
                            _logger.LogInformation("Транзакция успешно завершена для surveyId: {SurveyId}", request.SurveyId);
                            return Ok(new { 
                                success = true,
                                message = "Доступ к анкете успешно продлён",
                                surveyId = request.SurveyId
                            });
                        }
                        catch (PostgresException pgEx)
                        {
                            transaction.Rollback();
                            _logger.LogError(pgEx, "Ошибка PostgreSQL при обработке запроса. Код: {Code}, Сообщение: {Message}", 
                                pgEx.SqlState, pgEx.Message);
                            
                            return StatusCode(500, new { 
                                success = false,
                                message = "Ошибка базы данных",
                                error = pgEx.Message,
                                code = pgEx.SqlState
                            });
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            _logger.LogError(ex, "Ошибка при обработке транзакции");
                            return StatusCode(500, new { 
                                success = false,
                                message = "Ошибка при обработке запроса", 
                                error = ex.Message
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка в методе SaveSurveyExtensions");
                return StatusCode(500, new { 
                    success = false,
                    message = "Внутренняя ошибка сервера", 
                    error = ex.Message
                });
            }
        }

        private List<string> ValidateRequest(SurveyExtensionRequest request)
        {
            var errors = new List<string>();
            
            if (request.SurveyId <= 0) 
                errors.Add("Неверный ID анкеты");
            
            foreach (var extension in request.Extensions)
            {
                if (extension.OrganizationId <= 0) 
                    errors.Add($"Неверный ID организации: {extension.OrganizationId}");
                
                if (!DateTime.TryParse(extension.ExtendedUntil, out var endDate) || endDate <= DateTime.Today)
                    errors.Add($"Неверная дата окончания: {extension.ExtendedUntil}");
            }

            return errors;
        }

        private void ProcessExtensions(SurveyExtensionRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var extension in request.Extensions)
            {
                var endDate = DateTime.Parse(extension.ExtendedUntil);
                _logger.LogDebug("Обработка: surveyId={SurveyId}, organizationId={OrganizationId}, endDate={EndDate}", 
                    request.SurveyId, extension.OrganizationId, endDate);

                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO public.organization_survey (organization_id, id_survey, extended_until)
                      VALUES (@organizationId, @surveyId, @endDate)
                      ON CONFLICT (organization_id, id_survey) DO UPDATE
                      SET extended_until = EXCLUDED.extended_until",
                    connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@surveyId", request.SurveyId);
                    cmd.Parameters.AddWithValue("@organizationId", extension.OrganizationId);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    var affected = cmd.ExecuteNonQuery();
                    _logger.LogDebug("Обновлена запись продления в organization_survey, строк: {Count}", affected);
                }
            }
        }
    }

    public class SurveyExtensionRequest
    {
        [JsonPropertyName("surveyId")]
        public int SurveyId { get; set; }

        [JsonPropertyName("extensions")]
        public List<SurveyExtensionItemRequest> Extensions { get; set; } = new List<SurveyExtensionItemRequest>();
    }

    public class SurveyExtensionItemRequest
    {
        [JsonPropertyName("organizationId")]
        public int OrganizationId { get; set; }

        [JsonPropertyName("extendedUntil")]
        public string ExtendedUntil { get; set; } = string.Empty;
    }
}
