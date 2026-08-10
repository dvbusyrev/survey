using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;

[Authorize]
public class SurveyAnswersController : Controller
{
    private readonly SurveyService _surveyAnswersService;
    private readonly ILogger<SurveyAnswersController> _logger;

    public SurveyAnswersController(SurveyService surveyAnswersService, ILogger<SurveyAnswersController> logger)
    {
        _surveyAnswersService = surveyAnswersService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("surveys/{idSurvey:int}/organizations/{idOrganization:int}/answers/{type}/view")]
    public async Task<IActionResult> ViewAnswer(
        int idSurvey,
        int idOrganization,
        string type,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _surveyAnswersService.GetSurveyAnswerPageAsync(idSurvey, type, cancellationToken);
            if (model == null)
            {
                return NotFound("Анкета не найдена.");
            }

            return View("~/Views/Answer/view_answer.cshtml", model);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить ответы анкеты.", $"Ошибка при получении ответов анкеты {idSurvey}");
        }
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("surveys/{id:int}/answers/data")]
    public async Task<IActionResult> GetSurveyAnswers(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Json(await _surveyAnswersService.GetSurveyAnswersResponseAsync(id, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить ответы анкеты.", $"Ошибка при получении ответов анкеты {id}");
        }
    }
}
