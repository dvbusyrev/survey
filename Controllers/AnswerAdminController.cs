using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;

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
    public async Task<IActionResult> GetListAnswers(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        string? organizationIds = null,
        string? surveyIds = null,
        string? year = null,
        string? month = null,
        string? dateFrom = null,
        string? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View(
                "~/Web/Views/Answer/get_list_answers.cshtml",
                await _answerAdminService.GetAnswersPageAsync(
                    page,
                    sortBy,
                    sortDirection,
                    organizationIds,
                    surveyIds,
                    year,
                    month,
                    dateFrom,
                    dateTo,
                    cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить список ответов.", "Ошибка при получении списка ответов");
        }
    }

    [HttpGet("survey/{id:int}/signatures")]
    [HttpGet("surveys/{id:int}/signatures")]
    public async Task<IActionResult> GetSurveySignatures(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return BadRequest("Неверный ID анкеты");
        }

        try
        {
            return View(
                "~/Web/Views/Answer/survey_signatures.cshtml",
                await _answerAdminService.GetSignaturePageAsync(id, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить статус подписей.", $"Ошибка при получении статуса подписей анкеты {id}");
        }
    }

    [HttpGet("statistics")]
    public IActionResult OpenStatistics()
    {
        return View("~/Web/Views/Answer/open_statistics.cshtml");
    }

    [HttpGet("statistics/data")]
    public async Task<IActionResult> GetStatisticsData(CancellationToken cancellationToken = default)
    {
        try
        {
            return Json(await _answerAdminService.GetStatisticsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить данные статистики.", "Ошибка при получении данных статистики");
        }
    }
}
