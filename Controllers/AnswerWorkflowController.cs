using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.UseCases.Answers;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize]
public class AnswerWorkflowController : Controller
{
    private readonly AnswerService _answerService;
    private readonly ILogger<AnswerWorkflowController> _logger;

    public AnswerWorkflowController(
        AnswerService answerService,
        ILogger<AnswerWorkflowController> logger)
    {
        _answerService = answerService;
        _logger = logger;
    }

    [HttpPost("answers/create")]
    public async Task<IActionResult> InsertAnswer([FromBody] AnswerRecord answerData, CancellationToken cancellationToken = default)
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

        var accessResult = await EnsureAnswerSubmissionAccessAsync(
            answerData.IdSurvey, answerData.OrganizationId, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = await _answerService.InsertAnswerAsync(answerData, cancellationToken);
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

            return View("~/Web/Views/Answer/check_answers.cshtml", result.Model);
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return isAjaxRequest
                ? this.SafeError(ex, "Анкета уже подписана.", "Повторное сохранение ответа", StatusCodes.Status409Conflict)
                : this.SafeErrorView(ex, "Анкета уже подписана.", "Повторное сохранение ответа");
        }
        catch (Exception ex)
        {
            return isAjaxRequest
                ? this.SafeError(ex, "Не удалось сохранить ответы.", "Ошибка при сохранении ответа")
                : this.SafeErrorView(ex, "Не удалось сохранить ответы.", "Ошибка при сохранении ответа");
        }
    }

    [HttpPost("answers/draft")]
    public async Task<IActionResult> SaveDraftAnswer([FromBody] AnswerRecord answerData, CancellationToken cancellationToken = default)
    {
        if (answerData == null)
        {
            return BadRequest(new OperationResponse { Error = "Данные черновика отсутствуют." });
        }

        var accessResult = await EnsureAnswerSubmissionAccessAsync(
            answerData.IdSurvey, answerData.OrganizationId, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = await _answerService.SaveDraftAnswerAsync(answerData, cancellationToken);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return NotFound(new OperationResponse { Error = result.Error });
                }

                return BadRequest(new OperationResponse { Error = result.Error });
            }

            return Ok(new OperationResponse
            {
                Success = true,
                Message = "Черновик сохранён."
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить черновик.", "Ошибка при сохранении черновика ответа");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/{type?}")]
    public async Task<IActionResult> Answers(
        int idSurvey,
        int idOrganization = 0,
        string type = "regular",
        CancellationToken cancellationToken = default)
    {
        var includeAllOrganizationAnswers = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase)
            && _answerService.IsAdmin;

        if (!includeAllOrganizationAnswers)
        {
            var accessResult = await EnsureAnswerRecordAccessAsync(idSurvey, idOrganization, cancellationToken);
            if (accessResult != null)
            {
                return accessResult;
            }
        }

        try
        {
            var response = await _answerService.GetAnswersResponseAsync(
                idSurvey, idOrganization, type, includeAllOrganizationAnswers, cancellationToken);
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Json(response);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить ответы.", "Ошибка при обработке запроса ответов");
        }
    }

    [HttpGet("answers/{idSurvey:int}/{idOrganization:int}/content")]
    public async Task<IActionResult> AnswersContent(
        int idSurvey,
        int idOrganization,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerRecordAccessAsync(idSurvey, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var response = await _answerService.GetAnswersResponseAsync(
                idSurvey, idOrganization, "regular", false, cancellationToken);
            if (!response.Success || response.Survey == null)
            {
                return NotFound(response.Error ?? "Ответы не найдены.");
            }

            var model = new UserSurveyAnswersContentViewModel
            {
                Survey = response.Survey,
                OrganizationId = idOrganization,
                Answers = response.Answers
            };

            return PartialView("~/Web/Views/Survey/Partials/_UserSurveyAnswersContent.cshtml", model);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить ответы.", $"Ошибка при загрузке содержимого ответов {idSurvey} для организации {idOrganization}");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/edit")]
    public async Task<IActionResult> UpdateAnswer(
        [FromRoute] int idSurvey,
        [FromRoute] int idOrganization,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerRecordAccessAsync(idSurvey, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var model = await _answerService.GetUpdateAnswerPageAsync(idSurvey, idOrganization, cancellationToken);
            if (model == null)
            {
                return NotFound("Ответы не найдены");
            }

            return View("~/Web/Views/Answer/update_answer.cshtml", model);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось открыть редактирование ответа.", "Ошибка при загрузке страницы редактирования ответа");
        }
    }

    [HttpPost("answers/update")]
    public async Task<IActionResult> UpdateAnswerRecord(
        [FromBody] AnswerRecord answerData,
        CancellationToken cancellationToken = default)
    {
        if (answerData == null)
        {
            return BadRequest("Данные ответа отсутствуют.");
        }

        var accessResult = await EnsureAnswerRecordAccessAsync(
            answerData.IdSurvey, answerData.OrganizationId, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = await _answerService.UpdateAnswerAsync(answerData, cancellationToken);
            if (!result.Success)
            {
                if (result.NotFound)
                {
                    return NotFound(result.Error ?? "Запись для обновления не найдена.");
                }

                return BadRequest(result.Error ?? "Некорректные данные ответа.");
            }

            return View("~/Web/Views/Answer/check_answers.cshtml", result.Model);
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return this.SafeError(ex, "Анкета уже подписана.", "Повторное обновление ответа", StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось обновить ответ.", "Ошибка при обновлении ответа");
        }
    }

    private async Task<IActionResult?> EnsureAnswerSubmissionAccessAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        if (!_answerService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!await _answerService.CanSubmitAnswerAsync(surveyId, requestedOrganizationId, cancellationToken))
        {
            return Forbid();
        }

        return null;
    }

    private async Task<IActionResult?> EnsureAnswerRecordAccessAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        if (!_answerService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!await _answerService.CanAccessAnswerRecordAsync(surveyId, requestedOrganizationId, cancellationToken))
        {
            return Forbid();
        }

        return null;
    }
}
