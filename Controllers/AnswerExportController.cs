using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Web.Infrastructure;

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
    public async Task<IActionResult> CreatePdfReport(
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
            var result = await _answerExportService.CreatePdfReportAsync(idSurvey, idOrganization, cancellationToken);
            if (result == null)
            {
                return NotFound("Ответы не найдены");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать PDF.", $"Ошибка генерации PDF по анкете {idSurvey}");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/signed-archive")]
    public async Task<IActionResult> DownloadSignedArchive(
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
            var result = await _answerExportService.CreateSignedArchiveAsync(idSurvey, idOrganization, cancellationToken);
            if (result == null)
            {
                return NotFound("Ответы не найдены");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать архив с подписью.", $"Ошибка при создании архива с подписью для анкеты {idSurvey}");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/report/{type?}")]
    public async Task<IActionResult> CreateAnswerReport(
        int idSurvey,
        int idOrganization,
        string type = "file",
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureAnswerRecordAccessAsync(idSurvey, idOrganization, cancellationToken);
        if (accessResult != null)
        {
            return accessResult;
        }

        try
        {
            var result = await _answerExportService.CreateSurveyReportAsync(idSurvey, idOrganization, type, cancellationToken);
            if (result == null)
            {
                return NotFound("Не удалось создать отчет");
            }

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сформировать отчёт.", $"Ошибка при формировании отчёта по анкете {idSurvey}");
        }
    }

    [HttpGet("answers/{idSurvey}/{idOrganization}/archive")]
    public Task<IActionResult> CreateAnswerReportArchive(
        int idSurvey,
        int idOrganization,
        CancellationToken cancellationToken = default)
    {
        return CreateAnswerReport(idSurvey, idOrganization, "archive", cancellationToken);
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
}
