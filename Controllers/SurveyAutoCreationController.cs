using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
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
        return View("~/Web/Views/Survey/view_auto_creation.cshtml", model);
    }

    [HttpGet("settings/survey-creation/surveys")]
    [HttpGet("survey-auto-creation/surveys")]
    public async Task<IActionResult> GetSurveyOptions(CancellationToken cancellationToken)
    {
        var surveys = (await _surveyAutoCreationService.GetSurveyOptionsAsync(cancellationToken))
            .Select(static survey => new { id = survey.Id, name = survey.Name })
            .ToArray();
        return Json(surveys);
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
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить настройки автосоздания анкет.", "Ошибка при сохранении настроек автосоздания анкет");
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
