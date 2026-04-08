using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Answers;
using main_project.Models;

[Authorize]
public class AnswerWorkflowController : Controller
{
    private readonly AnswerAccessService _answerAccessService;
    private readonly AnswerWorkflowService _answerWorkflowService;
    private readonly ILogger<AnswerWorkflowController> _logger;

    public AnswerWorkflowController(
        AnswerAccessService answerAccessService,
        AnswerWorkflowService answerWorkflowService,
        ILogger<AnswerWorkflowController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerWorkflowService = answerWorkflowService;
        _logger = logger;
    }

    [HttpPost("answers/create")]
    [HttpPost("api/insert_answer")]
    public IActionResult insert_answer([FromBody] HistoryAnswer historyAnswerData)
    {
        var isAjaxRequest =
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || Request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (historyAnswerData == null)
        {
            return isAjaxRequest
                ? BadRequest(new { error = "Данные ответа отсутствуют." })
                : BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOrganizationAccess(historyAnswerData.organization_id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.InsertAnswer(historyAnswerData);
            if (model == null)
            {
                return isAjaxRequest
                    ? NotFound(new { error = "Анкета не найдена." })
                    : NotFound("Анкета не найдена");
            }

            if (isAjaxRequest)
            {
                return Ok(new
                {
                    success = true,
                    message = "Ответы успешно сохранены."
                });
            }

            return View("~/Views/Answer/check_answers.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении ответа");
            return isAjaxRequest
                ? StatusCode(500, new { error = $"Ошибка при сохранении ответа: {ex.Message}" })
                : View("Error", new ErrorViewModel { Message = $"Ошибка при сохранении ответа: {ex.Message}" });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/{type?}")]
    [HttpGet("Answer/answers/{idSurvey}/{idOrganization}/{type?}")]
    public IActionResult answers(int idSurvey, int idOrganization = 0, string type = "regular")
    {
        var includeAllOrganizationAnswers = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase)
            && _answerAccessService.IsAdmin;

        if (!includeAllOrganizationAnswers)
        {
            var accessResult = EnsureOrganizationAccess(idOrganization);
            if (accessResult != null)
            {
                return accessResult;
            }
        }

        try
        {
            var response = _answerWorkflowService.GetAnswersResponse(idSurvey, idOrganization, type, includeAllOrganizationAnswers);
            if (!response.Success)
            {
                return NotFound(new
                {
                    success = false,
                    error = response.Error
                });
            }

            return Json(new
            {
                success = true,
                survey = new
                {
                    id = response.Survey?.Id ?? idSurvey,
                    name = response.Survey?.Name ?? string.Empty,
                    description = response.Survey?.Description,
                    is_archive = response.Survey?.IsArchive ?? false,
                    csp = response.Survey?.Csp
                },
                answers = response.Answers.Select(answer => new
                {
                    id = answer.Id,
                    organization_id = answer.OrganizationId,
                    organization_name = answer.OrganizationName,
                    date = answer.Date,
                    answers = answer.Answers.Select(item => new
                    {
                        question_text = item.QuestionText,
                        rating = item.Rating,
                        comment = item.Comment
                    }),
                    is_signed = answer.IsSigned,
                    signature = answer.Signature
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке запроса ответов");
            return StatusCode(500, new
            {
                success = false,
                error = "Внутренняя ошибка сервера",
                details = ex.Message
            });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/edit")]
    [HttpPost("answers/{idSurvey}/{idOrganization}/edit")]
    [HttpPost("update_answer/{idSurvey}/{idOrganization}")]
    [HttpPost("Answer/update_answer/{idSurvey}/{idOrganization}")]
    public IActionResult update_answer([FromRoute] int idSurvey, [FromRoute] int idOrganization)
    {
        var accessResult = EnsureOrganizationAccess(idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.GetUpdateAnswerPage(idSurvey, idOrganization);
            if (model == null)
            {
                return NotFound("Ответы не найдены");
            }

            return View("~/Views/Answer/update_answer.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке страницы редактирования ответа");
            return StatusCode(500, "Произошла ошибка на сервере");
        }
    }

    [HttpPost("answers/update")]
    [HttpPost("update_answer_bd")]
    [HttpPost("Answer/update_answer_bd")]
    public IActionResult update_answer_bd([FromBody] HistoryAnswer historyAnswerData)
    {
        if (historyAnswerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOrganizationAccess(historyAnswerData.organization_id);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.UpdateAnswer(historyAnswerData);
            if (model == null)
            {
                return NotFound("Запись для обновления не найдена.");
            }

            return View("~/Views/Answer/check_answers.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении ответа");
            return View("Error", new ErrorViewModel { Message = $"Ошибка при обновлении ответа: {ex.Message}" });
        }
    }

    private IActionResult? EnsureOrganizationAccess(int requestedOrganizationId)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_answerAccessService.CanAccessOrganization(requestedOrganizationId))
        {
            return Forbid();
        }

        return null;
    }
}
