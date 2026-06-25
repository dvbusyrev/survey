using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Web.Infrastructure;

[Authorize]
public class AnswerSigningController : Controller
{
    private readonly IAnswerAccessService _answerAccessService;
    private readonly IAnswerSigningService _answerSigningService;
    private readonly ILogger<AnswerSigningController> _logger;

    public AnswerSigningController(
        IAnswerAccessService answerAccessService,
        IAnswerSigningService answerSigningService,
        ILogger<AnswerSigningController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerSigningService = answerSigningService;
        _logger = logger;
    }

    [HttpGet("signatures/{id}/{idOrganization}")]
    public async Task<IActionResult> GetSigningData(int id, int idOrganization, CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerRecordAccessAsync(id, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            return Json(await _answerSigningService.GetSigningDataAsync(id, idOrganization, cancellationToken));
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return this.SafeError(ex, "Анкета уже подписана.", "Повторное получение данных подписи", StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось получить данные для подписи.", "Ошибка при получении данных для подписи");
        }
    }

    [HttpPost("signatures/{id}/{idOrganization}")]
    public async Task<IActionResult> CspAnswer(
        [FromRoute] int id,
        [FromRoute] int idOrganization,
        [FromBody] AnswerSignatureSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerRecordAccessAsync(id, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Signature))
            {
                return BadRequest("Signature не может быть пустым.");
            }

            if (!await _answerSigningService.SaveSignatureAsync(id, idOrganization, request, cancellationToken))
            {
                return NotFound("Запись для обновления не найдена.");
            }

            return Ok("Запись успешно обновлена.");
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return this.SafeError(ex, "Анкета уже подписана.", "Повторное сохранение подписи", StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return this.SafeError(ex, "Некорректные данные подписи.", "Некорректные данные подписи", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить подпись.", "Ошибка при сохранении подписи ответа");
        }
    }

    [HttpGet("draft-signatures/{id}/{idOrganization}")]
    public async Task<IActionResult> GetDraftSigningData(int id, int idOrganization, CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerSubmissionAccessAsync(id, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            return Json(await _answerSigningService.GetDraftSigningDataAsync(id, idOrganization, cancellationToken));
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return this.SafeError(ex, "Черновик уже подписан.", "Повторное получение данных подписи черновика", StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось получить данные черновика для подписи.", "Ошибка при получении данных черновика для подписи");
        }
    }

    [HttpPost("draft-signatures/{id}/{idOrganization}")]
    public async Task<IActionResult> CspDraftAnswer(
        [FromRoute] int id,
        [FromRoute] int idOrganization,
        [FromBody] AnswerSignatureSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerSubmissionAccessAsync(id, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Signature))
            {
                return BadRequest("Signature не может быть пустым.");
            }

            if (!await _answerSigningService.SaveDraftSignatureAsync(id, idOrganization, request, cancellationToken))
            {
                return NotFound("Черновик для обновления не найден.");
            }

            return Ok("Черновик успешно подписан.");
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return this.SafeError(ex, "Черновик уже подписан.", "Повторное сохранение подписи черновика", StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return this.SafeError(ex, "Некорректные данные подписи.", "Некорректные данные подписи черновика", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сохранить подпись черновика.", "Ошибка при сохранении подписи черновика");
        }
    }

    private async Task<IActionResult?> EnsureAnswerRecordAccessAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!await _answerAccessService.CanAccessAnswerRecordAsync(surveyId, requestedOrganizationId, cancellationToken))
        {
            return Forbid();
        }

        return null;
    }

    private async Task<IActionResult?> EnsureAnswerSubmissionAccessAsync(
        int surveyId,
        int requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!await _answerAccessService.CanSubmitAnswerAsync(surveyId, requestedOrganizationId, cancellationToken))
        {
            return Forbid();
        }

        return null;
    }
}
