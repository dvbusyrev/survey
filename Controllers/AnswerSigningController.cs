using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Answers;

[Authorize]
public class AnswerSigningController : Controller
{
    private readonly AnswerAccessService _answerAccessService;
    private readonly AnswerSigningService _answerSigningService;
    private readonly ILogger<AnswerSigningController> _logger;

    public AnswerSigningController(
        AnswerAccessService answerAccessService,
        AnswerSigningService answerSigningService,
        ILogger<AnswerSigningController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerSigningService = answerSigningService;
        _logger = logger;
    }

    [HttpGet("signatures/{id}/{idOmsu}")]
    [HttpGet("get_signing_data/{id}/{idOmsu}")]
    [HttpGet("Answer/get_signing_data/{id}/{idOmsu}")]
    public IActionResult GetSigningData(int id, int idOmsu)
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            return Content(_answerSigningService.GetSigningData(id, idOmsu));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении данных для подписи");
            return StatusCode(500, "Ошибка при получении данных для подписи");
        }
    }

    [HttpPost("signatures/{id}/{idOmsu}")]
    [HttpPost("csp/{id}/{idOmsu}")]
    [HttpPost("Answer/csp/{id}/{idOmsu}")]
    public IActionResult CSP_answer([FromRoute] int id, [FromRoute] int idOmsu, [FromBody] JsonElement request)
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
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

            if (!_answerSigningService.SaveSignature(id, idOmsu, signature))
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

    private IActionResult? EnsureOmsuAccess(int requestedOmsuId)
    {
        if (!_answerAccessService.IsAuthenticated)
        {
            return Challenge();
        }

        if (!_answerAccessService.CanAccessOmsu(requestedOmsuId))
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
