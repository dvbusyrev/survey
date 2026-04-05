using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Answers;

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

    [HttpGet("answers/{idSurvey}/{idOmsu}/pdf")]
    [HttpGet("create_pdf_report/{idSurvey}/{idOmsu}")]
    [HttpGet("Answer/create_pdf_report/{idSurvey}/{idOmsu}")]
    public IActionResult CreatePdfReport(int idSurvey, int idOmsu)
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreatePdfReport(idSurvey, idOmsu);
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

    [HttpGet("answers/{idSurvey}/{idOmsu}/signed-archive")]
    [HttpGet("download_signed_archive/{idSurvey}/{idOmsu}")]
    [HttpGet("Answer/download_signed_archive/{idSurvey}/{idOmsu}")]
    public IActionResult DownloadSignedArchive(int idSurvey, int idOmsu)
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreateSignedArchive(idSurvey, idOmsu);
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

    [HttpGet("answers/{idSurvey}/{idOmsu}/report/{type?}")]
    [HttpGet("create_otchet_for_me/{idSurvey}/{idOmsu}/{type?}")]
    [HttpGet("Answer/create_otchet_for_me")]
    [HttpGet("Answer/create_otchet_for_me/{idSurvey}/{idOmsu}/{type?}")]
    public IActionResult create_otchet_for_me(int idSurvey, int idOmsu, string type = "file")
    {
        var accessResult = EnsureOmsuAccess(idOmsu);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = _answerExportService.CreateSurveyReport(idSurvey, idOmsu, type);
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

    [HttpGet("answers/{idSurvey}/{idOmsu}/archive")]
    [HttpGet("create_archiv_for_me/{idSurvey}/{idOmsu}")]
    [HttpGet("Answer/create_archiv_for_me/{idSurvey}/{idOmsu}")]
    public IActionResult create_archiv_for_me(int idSurvey, int idOmsu)
    {
        return create_otchet_for_me(idSurvey, idOmsu, "archiv");
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
}
