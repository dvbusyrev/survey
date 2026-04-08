using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Answers;

[Authorize]
public class AnswerExportController : Controller
{
    private readonly AnswerAccessService _answerAccessService;
    private readonly AnswerExportService _answerExportService;
    private readonly ILogger<AnswerExportController> _logger;

    public AnswerExportController(
        AnswerAccessService answerAccessService,
        AnswerExportService answerExportService,
        ILogger<AnswerExportController> logger)
    {
        _answerAccessService = answerAccessService;
        _answerExportService = answerExportService;
        _logger = logger;
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/pdf")]
    [HttpGet("create_pdf_report/{idSurvey}/{idOrganization}")]
    [HttpGet("Answer/create_pdf_report/{idSurvey}/{idOrganization}")]
    public IActionResult CreatePdfReport(int idSurvey, int idOrganization)
    {
        var accessResult = EnsureOrganizationAccess(idOrganization);
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
    [HttpGet("download_signed_archive/{idSurvey}/{idOrganization}")]
    [HttpGet("Answer/download_signed_archive/{idSurvey}/{idOrganization}")]
    public IActionResult DownloadSignedArchive(int idSurvey, int idOrganization)
    {
        var accessResult = EnsureOrganizationAccess(idOrganization);
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
    [HttpGet("create_answer_report/{idSurvey}/{idOrganization}/{type?}")]
    [HttpGet("Answer/create_answer_report")]
    [HttpGet("Answer/create_answer_report/{idSurvey}/{idOrganization}/{type?}")]
    [ActionName("create_answer_report")]
    public IActionResult CreateAnswerReport(int idSurvey, int idOrganization, string type = "file")
    {
        var accessResult = EnsureOrganizationAccess(idOrganization);
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
    [HttpGet("create_answer_report_archive/{idSurvey}/{idOrganization}")]
    [HttpGet("Answer/create_answer_report_archive/{idSurvey}/{idOrganization}")]
    [ActionName("create_answer_report_archive")]
    public IActionResult CreateAnswerReportArchive(int idSurvey, int idOrganization)
    {
        return CreateAnswerReport(idSurvey, idOrganization, "archive");
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
