using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Answers;
using MainProject.Models;

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
    public IActionResult InsertAnswer([FromBody] AnswerRecord answerData)
    {
        var isAjaxRequest =
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || Request.Headers.Accept.Any(value =>
                !string.IsNullOrWhiteSpace(value)
                && value.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (answerData == null)
        {
            return isAjaxRequest
                ? BadRequest(new OperationResponse { Error = "Данные ответа отсутствуют." })
                : BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureAnswerSubmissionAccess(answerData.IdSurvey, answerData.OrganizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerWorkflowService.InsertAnswer(answerData);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return isAjaxRequest
                        ? NotFound(new OperationResponse { Error = result.Error })
                        : NotFound(result.Error ?? "Анкета не найдена");
                }

                return isAjaxRequest
                    ? BadRequest(new OperationResponse { Error = result.Error })
                    : BadRequest(result.Error ?? "Некорректные данные ответа.");
            }

            if (isAjaxRequest)
            {
                return Ok(new OperationResponse
                {
                    Success = true,
                    Message = "Ответы успешно сохранены."
                });
            }

            return View("~/Views/Answer/check_answers.cshtml", result.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении ответа");
            return isAjaxRequest
                ? StatusCode(500, new OperationResponse { Error = $"Ошибка при сохранении ответа: {ex.Message}" })
                : View("Error", new ErrorViewModel { Message = $"Ошибка при сохранении ответа: {ex.Message}" });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/{type?}")]
    public IActionResult Answers(int idSurvey, int idOrganization = 0, string type = "regular")
    {
        var includeAllOrganizationAnswers = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase)
            && _answerAccessService.IsAdmin;

        if (!includeAllOrganizationAnswers)
        {
            var accessResult = EnsureAnswerRecordAccess(idSurvey, idOrganization);
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
                return NotFound(response);
            }

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке запроса ответов");
            return StatusCode(500, new SurveyAnswersResponse
            {
                Success = false,
                Error = "Внутренняя ошибка сервера",
                Details = ex.Message
            });
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/edit")]
    public IActionResult UpdateAnswer([FromRoute] int idSurvey, [FromRoute] int idOrganization)
    {
        var accessResult = EnsureAnswerRecordAccess(idSurvey, idOrganization);
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
    public IActionResult UpdateAnswerRecord([FromBody] AnswerRecord answerData)
    {
        if (answerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureAnswerRecordAccess(answerData.IdSurvey, answerData.OrganizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerWorkflowService.UpdateAnswer(answerData);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return NotFound(result.Error ?? "Запись для обновления не найдена.");
                }

                return BadRequest(result.Error ?? "Некорректные данные ответа.");
            }

            return View("~/Views/Answer/check_answers.cshtml", result.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении ответа");
            return View("Error", new ErrorViewModel { Message = $"Ошибка при обновлении ответа: {ex.Message}" });
        }
    }

    private IActionResult? EnsureAnswerSubmissionAccess(int surveyId, int requestedOrganizationId)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_answerAccessService.CanSubmitAnswer(surveyId, requestedOrganizationId))
        {
            return Forbid();
        }

        return null;
    }

    private IActionResult? EnsureAnswerRecordAccess(int surveyId, int requestedOrganizationId)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_answerAccessService.CanAccessAnswerRecord(surveyId, requestedOrganizationId))
        {
            return Forbid();
        }

        return null;
    }
}
