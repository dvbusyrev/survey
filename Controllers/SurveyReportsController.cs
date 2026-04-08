using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Services.Surveys;
using main_project.Infrastructure.Security;

[Authorize]
public class SurveyReportsController : Controller
{
    private readonly SurveyReportService _surveyReportService;
    private readonly ILogger<SurveyReportsController> _logger;

    public SurveyReportsController(SurveyReportService surveyReportService, ILogger<SurveyReportsController> logger)
    {
        _surveyReportService = surveyReportService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports")]
    [HttpGet("view_otchets")]
    [HttpGet("Survey/view_otchets")]
    public IActionResult view_otchets()
    {
        return View("~/Views/Survey/view_otchets.cshtml");
    }

    [HttpGet("reports/monthly/{id:int}")]
    [HttpGet("create_otchet_month/{id:int}")]
    [HttpGet("Survey/create_otchet_month/{id:int}")]
    public IActionResult create_otchet_month(int id, int idOrganization = 0, string type = "")
    {
        try
        {
            var result = _surveyReportService.CreateSurveyMonthlyReport(id, idOrganization);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании месячного отчёта по анкете {SurveyId}", id);
            return StatusCode(500, "Произошла ошибка при формировании отчета");
        }
    }

    [HttpGet("reports/monthly")]
    [HttpGet("create_otchetAll_month")]
    [HttpGet("Survey/create_otchetAll_month")]
    public IActionResult create_otchetAll_month()
    {
        try
        {
            var result = _surveyReportService.CreateAllMonthlyReport();
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании сводного месячного отчёта");
            return StatusCode(500, "Произошла ошибка при формировании отчета");
        }
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/quarterly/{kvartal}")]
    [HttpGet("reports/quarterly/{kvartal}/{year}")]
    [HttpGet("create_otchet_kvartal/{kvartal}")]
    [HttpGet("create_otchet_kvartal/{kvartal}/{year}")]
    [HttpGet("Survey/create_otchet_kvartal/{kvartal}")]
    [HttpGet("Survey/create_otchet_kvartal/{kvartal}/{year}")]
    public IActionResult create_otchet_kvartal(int kvartal, int year = 0)
    {
        try
        {
            var result = _surveyReportService.CreateQuarterlyReport(kvartal, year);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании квартального отчёта за {Quarter} квартал {Year}", kvartal, year);
            return StatusCode(500, "Произошла ошибка при формировании отчета");
        }
    }
}
