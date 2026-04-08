using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Services.Surveys;
using MainProject.Infrastructure.Security;

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
    [HttpGet("view_reports")]
    [HttpGet("Survey/view_reports")]
    [ActionName("view_reports")]
    public IActionResult ViewReports()
    {
        return View("~/Views/Survey/view_reports.cshtml");
    }

    [HttpGet("reports/monthly/{id:int}")]
    [HttpGet("create_monthly_report/{id:int}")]
    [HttpGet("Survey/create_monthly_report/{id:int}")]
    [ActionName("create_monthly_report")]
    public IActionResult CreateMonthlyReport(int id, int idOrganization = 0, string type = "")
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
    [HttpGet("create_monthly_summary_report")]
    [HttpGet("Survey/create_monthly_summary_report")]
    [ActionName("create_monthly_summary_report")]
    public IActionResult CreateMonthlySummaryReport()
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
    [HttpGet("reports/quarterly/{quarter}")]
    [HttpGet("reports/quarterly/{quarter}/{year}")]
    [HttpGet("create_quarterly_report/{quarter}")]
    [HttpGet("create_quarterly_report/{quarter}/{year}")]
    [HttpGet("Survey/create_quarterly_report/{quarter}")]
    [HttpGet("Survey/create_quarterly_report/{quarter}/{year}")]
    [ActionName("create_quarterly_report")]
    public IActionResult CreateQuarterlyReport(int quarter, int year = 0)
    {
        try
        {
            var result = _surveyReportService.CreateQuarterlyReport(quarter, year);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании квартального отчёта за {Quarter} квартал {Year}", quarter, year);
            return StatusCode(500, "Произошла ошибка при формировании отчета");
        }
    }
}
