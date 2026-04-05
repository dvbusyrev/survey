using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Answers;
using main_project.Infrastructure.Security;

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
    public IActionResult get_list_answers()
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
    [HttpGet("get_list_csp/{id:int}")]
    [HttpPost("get_list_csp/{id:int}")]
    [HttpGet("Answer/get_list_csp/{id:int}")]
    [HttpPost("Answer/get_list_csp/{id:int}")]
    public IActionResult get_list_csp(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Неверный ID анкеты");
        }

        try
        {
            return View("~/Views/Answer/get_list_csp.cshtml", _answerAdminService.GetSignaturePage(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса подписей для анкеты {SurveyId}", id);
            return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    [HttpGet("statistics")]
    [HttpGet("open_statistic")]
    [HttpGet("Answer/open_statistic")]
    public IActionResult open_statistic()
    {
        return View("~/Views/Answer/open_statistic.cshtml");
    }

    [HttpGet("statistics/data")]
    [HttpGet("get_data_statistic")]
    [HttpGet("Answer/get_data_statistic")]
    public IActionResult get_data_statistic()
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
