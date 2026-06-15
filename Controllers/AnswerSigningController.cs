using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;

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
    public IActionResult GetSigningData(int id, int idOrganization)
    {
        var accessResult = EnsureAnswerRecordAccess(id, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            return Json(_answerSigningService.GetSigningData(id, idOrganization));
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении данных для подписи");
            return StatusCode(500, "Ошибка при получении данных для подписи");
        }
    }

    [HttpPost("signatures/{id}/{idOrganization}")]
    public IActionResult CspAnswer([FromRoute] int id, [FromRoute] int idOrganization, [FromBody] AnswerSignatureSaveRequest request)
    {
        var accessResult = EnsureAnswerRecordAccess(id, idOrganization);
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

            if (!_answerSigningService.SaveSignature(id, idOrganization, request))
            {
                return NotFound("Запись для обновления не найдена.");
            }

            return Ok("Запись успешно обновлена.");
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении подписи ответа");
            return StatusCode(500, $"Ошибка при обновлении ответа: {ex.Message}");
        }
    }

    [HttpGet("draft-signatures/{id}/{idOrganization}")]
    public IActionResult GetDraftSigningData(int id, int idOrganization)
    {
        var accessResult = EnsureAnswerSubmissionAccess(id, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            return Json(_answerSigningService.GetDraftSigningData(id, idOrganization));
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении данных черновика для подписи");
            return StatusCode(500, "Ошибка при получении данных черновика для подписи");
        }
    }

    [HttpPost("draft-signatures/{id}/{idOrganization}")]
    public IActionResult CspDraftAnswer([FromRoute] int id, [FromRoute] int idOrganization, [FromBody] AnswerSignatureSaveRequest request)
    {
        var accessResult = EnsureAnswerSubmissionAccess(id, idOrganization);
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

            if (!_answerSigningService.SaveDraftSignature(id, idOrganization, request))
            {
                return NotFound("Черновик для обновления не найден.");
            }

            return Ok("Черновик успешно подписан.");
        }
        catch (MainProject.Application.UseCases.Answers.AnswerAlreadySignedException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении подписи черновика ответа");
            return StatusCode(500, $"Ошибка при обновлении черновика: {ex.Message}");
        }
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
}
