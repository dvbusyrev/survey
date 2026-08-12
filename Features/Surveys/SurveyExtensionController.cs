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

    [HttpGet("survey/{surveyId:int}/assigned-organizations")]
    public async Task<IActionResult> GetAssignedOrganizations(int surveyId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _surveyAdminService.GetAssignedOrganizationsForExtensionAsync(
                surveyId,
                cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(
                ex,
                "Не удалось загрузить назначенные организации.",
                $"Ошибка получения организаций анкеты {surveyId} для продления");
        }
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

    [HttpPost("survey/{surveyId:int}/extensions/{organizationId:int}/period")]
    public async Task<IActionResult> UpdateExtensionPeriod(
        int surveyId,
        int organizationId,
        [FromBody] SurveyAssignmentPeriodRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.UpdateExtensionPeriodAsync(
                surveyId,
                organizationId,
                request,
                cancellationToken);

            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message });
            }

            var error = new { success = false, message = result.Message };
            return string.Equals(result.Code, "extension_not_found", StringComparison.Ordinal)
                ? NotFound(error)
                : BadRequest(error);
        }
        catch (Exception ex)
        {
            return this.SafeError(
                ex,
                "Не удалось изменить период продления.",
                $"Ошибка при изменении периода продления анкеты {surveyId} для организации {organizationId}");
        }
    }

    [HttpPost("survey/{surveyId:int}/extensions/{organizationId:int}/delete")]
    public async Task<IActionResult> DeleteExtension(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAdminService.DeleteExtensionAsync(
                surveyId,
                organizationId,
                cancellationToken);

            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message });
            }

            var error = new { success = false, message = result.Message };
            if (string.Equals(result.Code, "extension_not_found", StringComparison.Ordinal))
            {
                return NotFound(error);
            }

            return BadRequest(error);
        }
        catch (Exception ex)
        {
            return this.SafeError(
                ex,
                "Не удалось удалить продление.",
                $"Ошибка при удалении продления анкеты {surveyId} для организации {organizationId}");
        }
    }
}
