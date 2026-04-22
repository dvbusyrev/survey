using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Security;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyReportsController : Controller
{
    private readonly ISurveyReportService _surveyReportService;
    private readonly ILogger<SurveyReportsController> _logger;

    public SurveyReportsController(ISurveyReportService surveyReportService, ILogger<SurveyReportsController> logger)
    {
        _surveyReportService = surveyReportService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports")]
    public IActionResult ViewReports()
    {
        var model = new ReportsPageViewModel
        {
            AvailableYears = _surveyReportService.GetAvailableReportYears()
        };

        return View("~/Web/Views/Survey/view_reports.cshtml", model);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/monthly/{id:int}")]
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

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/monthly")]
    public IActionResult CreateMonthlySummaryReport(int month, int year)
    {
        try
        {
            var result = _surveyReportService.CreateAllMonthlyReport(month, year);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
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
    public IActionResult CreateQuarterlyReport(int quarter, int year = 0)
    {
        try
        {
            var result = _surveyReportService.CreateQuarterlyReport(quarter, year);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании квартального отчёта за {Quarter} квартал {Year}", quarter, year);
            return StatusCode(500, "Произошла ошибка при формировании отчета");
        }
    }
}
