using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using Npgsql;
using System.Text.Json;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyExtensionController : Controller
{
    private readonly SurveyService _surveyAdminService;
    private readonly ILogger<SurveyExtensionController> _logger;

    public SurveyExtensionController(SurveyService surveyAdminService, ILogger<SurveyExtensionController> logger)
    {
        _surveyAdminService = surveyAdminService;
        _logger = logger;
    }

    [HttpPost]
    [Route("survey-extensions")]
    [Route("survey_extensions")]
    public async Task<IActionResult> SaveSurveyExtensions([FromBody] SurveyExtensionRequest? request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Получен запрос на продление анкеты: {Request}", JsonSerializer.Serialize(request));

        if (request == null)
        {
            return BadRequest(new { success = false, message = "Данные для продления не предоставлены." });
        }

        try
        {
            var result = await _surveyAdminService.SaveExtensionsAsync(request, cancellationToken);
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
                return this.SafeError(
                    new InvalidOperationException(result.Error ?? result.Message),
                    "Не удалось продлить анкету.",
                    $"Ошибка продления анкеты с кодом {result.Code}");
            }

            return this.SafeError(
                new InvalidOperationException(result.Error ?? result.Message),
                "Не удалось продлить анкету.",
                "Неизвестная ошибка продления анкеты");
        }
        catch (PostgresException pgEx)
        {
            return this.SafeError(pgEx, "Не удалось продлить анкету.", "Ошибка PostgreSQL при продлении анкеты");
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось продлить анкету.", "Критическая ошибка при продлении анкеты");
        }
    }
}
