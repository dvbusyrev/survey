using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using main_project.Infrastructure.Database;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace main_project.Controllers
{
    public class AeController : Controller
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<AeController> _logger;

        public AeController(IDbConnectionFactory connectionFactory, ILogger<AeController> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        [HttpPost]
        [Route("prodlenie_omsus")]
        public IActionResult prodlenie_omsus([FromBody] ExtensionRequest request)
        {
            _logger.LogInformation("Получен запрос на продление анкеты: {Request}", JsonSerializer.Serialize(request));

            try
            {
                if (request == null || request.extensions == null || request.extensions.Count == 0)
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
                            _logger.LogInformation("Транзакция успешно завершена для surveyId: {SurveyId}", request.survey_id);
                            return Ok(new { 
                                success = true,
                                message = "Доступ к анкете успешно продлён",
                                survey_id = request.survey_id
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
                _logger.LogError(ex, "Критическая ошибка в методе prodlenie_omsus");
                return StatusCode(500, new { 
                    success = false,
                    message = "Внутренняя ошибка сервера", 
                    error = ex.Message
                });
            }
        }

        private List<string> ValidateRequest(ExtensionRequest request)
        {
            var errors = new List<string>();
            
            if (request.survey_id <= 0) 
                errors.Add("Неверный ID анкеты");
            
            foreach (var ext in request.extensions)
            {
                if (ext.omsu_id <= 0) 
                    errors.Add($"Неверный ID организации: {ext.omsu_id}");
                
                if (!DateTime.TryParse(ext.new_end_date, out var endDate) || endDate <= DateTime.Today)
                    errors.Add($"Неверная дата окончания: {ext.new_end_date}");
            }

            return errors;
        }

        private void ProcessExtensions(ExtensionRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var ext in request.extensions)
            {
                var endDate = DateTime.Parse(ext.new_end_date);
                _logger.LogDebug("Обработка: surveyId={SurveyId}, omsuId={OmsuId}, endDate={EndDate}", 
                    request.survey_id, ext.omsu_id, endDate);

                // 1. Добавление записи о продлении (с использованием RETURNING для получения id)
                using (var cmd = new NpgsqlCommand(
                    @"INSERT INTO access_extensions 
                    (id_survey, id_omsu, new_end_date, created_at)
                    VALUES (@surveyId, @omsuId, @endDate, @createdAt)
                    RETURNING id", 
                    connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@surveyId", request.survey_id);
                    cmd.Parameters.AddWithValue("@omsuId", ext.omsu_id);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    cmd.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    
                    var newId = cmd.ExecuteScalar();
                    _logger.LogDebug("Добавлена запись в access_extensions с ID: {Id}", newId);
                }

                // 2. Обновление даты окончания анкеты
                using (var cmd = new NpgsqlCommand(
                    @"UPDATE surveys 
                    SET date_close = @endDate 
                    WHERE id_survey = @surveyId", 
                    connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@surveyId", request.survey_id);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    
                    int affected = cmd.ExecuteNonQuery();
                    _logger.LogDebug("Обновлено анкет: {Count}", affected);
                }
            }
        }
    }

    public class ExtensionRequest
    {
        public int survey_id { get; set; }
        public List<ExtensionItem> extensions { get; set; } = new List<ExtensionItem>();
    }

    public class ExtensionItem
    {
        public int omsu_id { get; set; }
        public string new_end_date { get; set; } = string.Empty;
    }
}
