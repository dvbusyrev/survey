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
    [HttpPost("api/insert_answer")]
    [ActionName("insert_answer")]
    public IActionResult InsertAnswer([FromBody] HistoryAnswer answerData)
    {
        var isAjaxRequest =
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || Request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (answerData == null)
        {
            return isAjaxRequest
                ? BadRequest(new OperationResponse { Error = "Данные ответа отсутствуют." })
                : BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOrganizationAccess(answerData.OrganizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.InsertAnswer(answerData);
            if (model == null)
            {
                return isAjaxRequest
                    ? NotFound(new OperationResponse { Error = "Анкета не найдена." })
                    : NotFound("Анкета не найдена");
            }

            if (isAjaxRequest)
            {
                return Ok(new OperationResponse
                {
                    Success = true,
                    Message = "Ответы успешно сохранены."
                });
            }

            return View("~/Views/Answer/check_answers.cshtml", model);
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
    [HttpGet("Answer/answers/{idSurvey}/{idOrganization}/{type?}")]
    [ActionName("answers")]
    public IActionResult Answers(int idSurvey, int idOrganization = 0, string type = "regular")
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
    [HttpPost("answers/{idSurvey}/{idOrganization}/edit")]
    [HttpPost("update_answer/{idSurvey}/{idOrganization}")]
    [HttpPost("Answer/update_answer/{idSurvey}/{idOrganization}")]
    [ActionName("update_answer")]
    public IActionResult UpdateAnswer([FromRoute] int idSurvey, [FromRoute] int idOrganization)
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
    [ActionName("update_answer_bd")]
    public IActionResult UpdateAnswerBd([FromBody] HistoryAnswer answerData)
    {
        if (answerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = EnsureOrganizationAccess(answerData.OrganizationId);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = _answerWorkflowService.UpdateAnswer(answerData);
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
