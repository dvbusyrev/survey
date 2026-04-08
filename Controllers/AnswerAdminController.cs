using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Answers;
using MainProject.Infrastructure.Security;

[Authorize(Roles = AppRoles.Admin)]
public class AnswerAdminController : Controller
{
    private readonly AnswerAdminService _answerAdminService;
    private readonly ILogger<AnswerAdminController> _logger;

    public AnswerAdminController(AnswerAdminService answerAdminService, ILogger<AnswerAdminController> logger)
    {
        _answerAdminService = answerAdminService;
        _logger = logger;
    }

    [HttpGet("surveys/answers")]
    [HttpGet("get_list_answers")]
    [HttpGet("Answer/get_list_answers")]
    [HttpGet("list_answers_users")]
    [ActionName("get_list_answers")]
    public IActionResult GetListAnswers()
    {
        try
        {
            return View("~/Views/Answer/get_list_answers.cshtml", _answerAdminService.GetAnswersPage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка ответов");
            return StatusCode(500, "Произошла ошибка на сервере");
        }
    }

    [HttpGet("surveys/{id:int}/signatures")]
    [HttpPost("surveys/{id:int}/signatures")]
    [HttpGet("get_survey_signatures/{id:int}")]
    [HttpPost("get_survey_signatures/{id:int}")]
    [HttpGet("Answer/get_survey_signatures/{id:int}")]
    [HttpPost("Answer/get_survey_signatures/{id:int}")]
    [ActionName("get_survey_signatures")]
    public IActionResult GetSurveySignatures(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Неверный ID анкеты");
        }

        try
        {
            return View("~/Views/Answer/survey_signatures.cshtml", _answerAdminService.GetSignaturePage(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса подписей для анкеты {SurveyId}", id);
            return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    [HttpGet("statistics")]
    [HttpGet("open_statistics")]
    [HttpGet("Answer/open_statistics")]
    [ActionName("open_statistics")]
    public IActionResult OpenStatistics()
    {
        return View("~/Views/Answer/open_statistics.cshtml");
    }

    [HttpGet("statistics/data")]
    [HttpGet("get_statistics_data")]
    [HttpGet("Answer/get_statistics_data")]
    [ActionName("get_statistics_data")]
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
