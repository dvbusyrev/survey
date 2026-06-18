using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;

[Authorize(Roles = AppRoles.Admin)]
public class SurveyAutoCreationController : Controller
{
    private readonly ISurveyAutoCreationService _surveyAutoCreationService;
    private readonly ILogger<SurveyAutoCreationController> _logger;

    public SurveyAutoCreationController(
        ISurveyAutoCreationService surveyAutoCreationService,
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
            _logger.LogError(ex, "Ошибка при сохранении настроек автосоздания анкет");
            return StatusCode(500, new { success = false, message = "Не удалось сохранить настройки автосоздания анкет." });
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
            _logger.LogError(ex, "Ошибка при запуске автосоздания анкет");
            return StatusCode(500, new { success = false, message = "Не удалось запустить автосоздание анкет." });
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
            _logger.LogError(ex, "Ошибка при остановке автосоздания анкет");
            return StatusCode(500, new { success = false, message = "Не удалось остановить автосоздание анкет." });
        }
    }
}
