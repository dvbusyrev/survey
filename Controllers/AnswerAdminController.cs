using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Security;

[Authorize(Roles = AppRoles.Admin)]
public class AnswerAdminController : Controller
{
    private readonly IAnswerAdminService _answerAdminService;
    private readonly ILogger<AnswerAdminController> _logger;

    public AnswerAdminController(IAnswerAdminService answerAdminService, ILogger<AnswerAdminController> logger)
    {
        _answerAdminService = answerAdminService;
        _logger = logger;
    }

    [HttpGet("survey/answer")]
    [HttpGet("surveys/answers")]
    public IActionResult GetListAnswers(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null)
    {
        try
        {
            return View(
                "~/Web/Views/Answer/get_list_answers.cshtml",
                _answerAdminService.GetAnswersPage(
                    page,
                    sortBy,
                    sortDirection,
                    organizationIds,
                    surveyIds,
                    year,
                    month,
                    dateFrom,
                    dateTo));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка ответов");
            return StatusCode(500, "Произошла ошибка на сервере");
        }
    }

    [HttpGet("survey/{id:int}/signatures")]
    [HttpGet("surveys/{id:int}/signatures")]
    public IActionResult GetSurveySignatures(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Неверный ID анкеты");
        }

        try
        {
            return View("~/Web/Views/Answer/survey_signatures.cshtml", _answerAdminService.GetSignaturePage(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса подписей для анкеты {SurveyId}", id);
            return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    [HttpGet("statistics")]
    public IActionResult OpenStatistics()
    {
        return View("~/Web/Views/Answer/open_statistics.cshtml");
    }

    [HttpGet("statistics/data")]
    public IActionResult GetStatisticsData()
    {
        try
        {
            return Json(_answerAdminService.GetStatistics());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении данных статистики");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
