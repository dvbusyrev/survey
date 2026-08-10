using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.UseCases.Answers;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;

[Authorize(Roles = AppRoles.Admin)]
public class AnswerAdminController : Controller
{
    private readonly AnswerService _answerService;
    private readonly ILogger<AnswerAdminController> _logger;

    public AnswerAdminController(AnswerService answerService, ILogger<AnswerAdminController> logger)
    {
        _answerService = answerService;
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
                "~/Views/Answer/get_list_answers.cshtml",
                await _answerService.GetAnswersPageAsync(
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
            return BadRequest("Некорректный идентификатор анкеты.");
        }

        try
        {
            return View(
                "~/Views/Answer/survey_signatures.cshtml",
                await _answerService.GetSignaturePageAsync(id, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить статус подписей.", $"Ошибка при получении статуса подписей анкеты {id}");
        }
    }

    [HttpPost("answers/{id:int}/delete")]
    public async Task<IActionResult> DeleteAnswer(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return BadRequest(new { success = false, message = "Некорректный идентификатор ответа." });
        }

        try
        {
            var result = await _answerService.DeleteAnswerAsync(id, cancellationToken);
            var payload = new
            {
                success = result.Success,
                message = result.Message
            };

            if (result.Success)
            {
                return Ok(payload);
            }

            return result.Code switch
            {
                "answer_not_found" => NotFound(payload),
                "survey_inactive" => Conflict(payload),
                _ => BadRequest(payload)
            };
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось удалить ответ.", $"Ошибка при удалении ответа {id}");
        }
    }

    [HttpGet("statistics")]
    public IActionResult OpenStatistics()
    {
        return View("~/Views/Answer/open_statistics.cshtml");
    }

    [HttpGet("statistics/data")]
    public async Task<IActionResult> GetStatisticsData(CancellationToken cancellationToken = default)
    {
        try
        {
            return Json(await _answerService.GetStatisticsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить данные статистики.", "Ошибка при получении данных статистики");
        }
    }
}
