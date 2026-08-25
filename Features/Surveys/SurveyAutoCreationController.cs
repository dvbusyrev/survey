using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.External.Calendar;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyAutoCreationController : Controller
{
    private readonly SurveyService _surveyAutoCreationService;
    private readonly ILogger<SurveyAutoCreationController> _logger;

    public SurveyAutoCreationController(
        SurveyService surveyAutoCreationService,
        ILogger<SurveyAutoCreationController> logger)
    {
        _surveyAutoCreationService = surveyAutoCreationService;
        _logger = logger;
    }

    [HttpGet("settings/survey-creation")]
    [HttpGet("survey-auto-creation")]
    public async Task<IActionResult> ViewPage(CancellationToken cancellationToken)
    {
        var model = await _surveyAutoCreationService.GetPageModelAsync(cancellationToken);
        return View("~/Views/Survey/view_auto_creation.cshtml", model);
    }

    [HttpGet("settings/survey-creation/templates")]
    [HttpGet("survey-auto-creation/templates")]
    public async Task<IActionResult> GetTemplateOptions(CancellationToken cancellationToken)
    {
        var templates = (await _surveyAutoCreationService.GetTemplateOptionsAsync(cancellationToken))
            .Select(static template => new { id = template.Id, name = template.Name })
            .ToArray();
        return Json(templates);
    }

    [HttpPost("settings/survey-creation/preview")]
    [HttpPost("survey-auto-creation/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(
        [FromBody] SurveyAutoCreationPreviewRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAutoCreationService.GetSchedulePreviewAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(result);
        }
        catch (ProductionCalendarUnavailableException ex)
        {
            return this.SafeError(
                ex,
                ProductionCalendarUnavailableException.UserMessage,
                "Производственный календарь недоступен при расчёте автосоздания анкет",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось рассчитать календарь действия.", "Ошибка расчёта календаря автосоздания анкет");
        }
    }

    [HttpPost("settings/survey-creation/save")]
    [HttpPost("survey-auto-creation/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAutoCreationService.SaveAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, isEnabled = result.IsEnabled });
        }
        catch (ProductionCalendarUnavailableException ex)
        {
            return this.SafeError(
                ex,
                ProductionCalendarUnavailableException.UserMessage,
                "Производственный календарь недоступен при применении настроек автосоздания анкет",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось применить настройки автосоздания анкет.", "Ошибка при применении настроек автосоздания анкет");
        }
    }

    [HttpPost("settings/survey-creation/start")]
    [HttpPost("survey-auto-creation/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start([FromBody] SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAutoCreationService.StartAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, isEnabled = result.IsEnabled });
        }
        catch (ProductionCalendarUnavailableException ex)
        {
            return this.SafeError(
                ex,
                ProductionCalendarUnavailableException.UserMessage,
                "Производственный календарь недоступен при запуске автосоздания анкет",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось запустить автосоздание анкет.", "Ошибка при запуске автосоздания анкет");
        }
    }

    [HttpPost("settings/survey-creation/stop")]
    [HttpPost("survey-auto-creation/stop")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _surveyAutoCreationService.StopAsync(cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, message = result.Message, isEnabled = result.IsEnabled });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось остановить автосоздание анкет.", "Ошибка при остановке автосоздания анкет");
        }
    }
}
