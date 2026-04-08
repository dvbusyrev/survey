using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Surveys;
using main_project.Infrastructure.Security;

[Authorize]
public class SurveyAnswersController : Controller
{
    private readonly SurveyAnswersService _surveyAnswersService;
    private readonly ILogger<SurveyAnswersController> _logger;

    public SurveyAnswersController(SurveyAnswersService surveyAnswersService, ILogger<SurveyAnswersController> logger)
    {
        _surveyAnswersService = surveyAnswersService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("view_answer/{idSurvey}/{idOrganization}/{type}")]
    [HttpPost("view_answer/{idSurvey}/{idOrganization}/{type}")]
    [HttpGet("Survey/view_answer/{idSurvey}/{idOrganization}/{type}")]
    [HttpPost("Survey/view_answer/{idSurvey}/{idOrganization}/{type}")]
    public IActionResult ViewAnswer(int idSurvey, int idOrganization, string type)
    {
        try
        {
            var model = _surveyAnswersService.GetSurveyAnswerPage(idSurvey, type);
            if (model == null)
            {
                return NotFound("Анкета не найдена");
            }

            return View("~/Views/Answer/view_answer.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении ответов для анкеты {SurveyId}", idSurvey);
            return StatusCode(500, "Произошла ошибка при загрузке данных");
        }
    }

    [HttpGet("Survey/GetSurveyAnswers")]
    public IActionResult GetSurveyAnswers(int id)
    {
        try
        {
            return Json(_surveyAnswersService.GetSurveyAnswersResponse(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении ответов анкеты {SurveyId}", id);
            return Json(new
            {
                success = false,
                error = "Внутренняя ошибка сервера",
                detail = ex.Message
            });
        }
    }
}
