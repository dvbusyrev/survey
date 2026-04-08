using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Admin;
using Npgsql;
using System.Text.Json;

namespace MainProject.Controllers;

public class SurveyExtensionController : Controller
{
    private readonly SurveyExtensionService _surveyExtensionService;
    private readonly ILogger<SurveyExtensionController> _logger;

    public SurveyExtensionController(SurveyExtensionService surveyExtensionService, ILogger<SurveyExtensionController> logger)
    {
        _surveyExtensionService = surveyExtensionService;
        _logger = logger;
    }

    [HttpPost]
    [Route("survey-extensions")]
    [Route("survey_extensions")]
    public IActionResult SaveSurveyExtensions([FromBody] SurveyExtensionRequest request)
    {
        _logger.LogInformation("Получен запрос на продление анкеты: {Request}", JsonSerializer.Serialize(request));

        if (request == null)
        {
            return BadRequest(new { success = false, message = "Необходимо предоставить данные для продления" });
        }

        try
        {
            var result = _surveyExtensionService.SaveExtensions(request);
            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    surveyId = request.SurveyId
                });
            }

            if (result.Errors.Count > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errors = result.Errors
                });
            }

            if (!string.IsNullOrWhiteSpace(result.Code))
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = result.Message,
                    error = result.Error,
                    code = result.Code
                });
            }

            return StatusCode(500, new
            {
                success = false,
                message = result.Message,
                error = result.Error
            });
        }
        catch (PostgresException pgEx)
        {
            _logger.LogError(pgEx, "Ошибка PostgreSQL при обработке запроса продления");
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка базы данных",
                error = pgEx.Message,
                code = pgEx.SqlState
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в методе SaveSurveyExtensions");
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера",
                error = ex.Message
            });
        }
    }
}
