using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;

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
            return Content(_answerSigningService.GetSigningData(id, idOrganization));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении данных для подписи");
            return StatusCode(500, "Ошибка при получении данных для подписи");
        }
    }

    [HttpPost("signatures/{id}/{idOrganization}")]
    public IActionResult CspAnswer([FromRoute] int id, [FromRoute] int idOrganization, [FromBody] JsonElement request)
    {
        var accessResult = EnsureAnswerRecordAccess(id, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var signature = ExtractSignature(request);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return BadRequest("Signature не может быть пустым.");
            }

            if (!_answerSigningService.SaveSignature(id, idOrganization, signature))
            {
                return NotFound("Запись для обновления не найдена.");
            }

            return Ok("Запись успешно обновлена.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении подписи ответа");
            return StatusCode(500, $"Ошибка при обновлении ответа: {ex.Message}");
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

    private static string? ExtractSignature(JsonElement request)
    {
        if (request.ValueKind == JsonValueKind.String)
        {
            return request.GetString();
        }

        if (request.ValueKind == JsonValueKind.Object
            && request.TryGetProperty("signature", out var signatureElement)
            && signatureElement.ValueKind == JsonValueKind.String)
        {
            return signatureElement.GetString();
        }

        return null;
    }
}
