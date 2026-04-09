using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;

[Authorize]
public class AnswerExportController : Controller
{
    private readonly IAnswerAccessService _answerAccessService;
    private readonly IAnswerExportService _answerExportService;
    private readonly ILogger<AnswerExportController> _logger;

    public AnswerExportController(
        IAnswerAccessService answerAccessService,
        IAnswerExportService answerExportService,
        ILogger<AnswerExportController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerExportService = answerExportService;
        _logger = logger;
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/pdf")]
    public IActionResult CreatePdfReport(int idSurvey, int idOrganization)
    {
        var accessResult = EnsureAnswerRecordAccess(idSurvey, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreatePdfReport(idSurvey, idOrganization);
            if (result == null)
            {
                return NotFound("Ответы не найдены");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации PDF по анкете {SurveyId}", idSurvey);
            return StatusCode(500, "Ошибка при создании PDF");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/signed-archive")]
    public IActionResult DownloadSignedArchive(int idSurvey, int idOrganization)
    {
        var accessResult = EnsureAnswerRecordAccess(idSurvey, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreateSignedArchive(idSurvey, idOrganization);
            if (result == null)
            {
                return NotFound("Ответы не найдены");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании архива с подписью для анкеты {SurveyId}", idSurvey);
            return StatusCode(500, "Ошибка при создании архива");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/report/{type?}")]
    public IActionResult CreateAnswerReport(int idSurvey, int idOrganization, string type = "file")
    {
        var accessResult = EnsureAnswerRecordAccess(idSurvey, idOrganization);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreateSurveyReport(idSurvey, idOrganization, type);
            if (result == null)
            {
                return NotFound("Не удалось сформировать отчет");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании отчета по анкете {SurveyId}", idSurvey);
            return StatusCode(500, "Ошибка при формировании отчета");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/archive")]
    public IActionResult CreateAnswerReportArchive(int idSurvey, int idOrganization)
    {
        return CreateAnswerReport(idSurvey, idOrganization, "archive");
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
