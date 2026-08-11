using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Security;
using MainProject.Application.UseCases.Surveys;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize]
public class SurveyReportsController : Controller
{
    private readonly SurveyService _surveyReportService;
    private readonly ILogger<SurveyReportsController> _logger;

    public SurveyReportsController(SurveyService surveyReportService, ILogger<SurveyReportsController> logger)
    {
        _surveyReportService = surveyReportService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports")]
    public async Task<IActionResult> ViewReports(CancellationToken cancellationToken = default)
    {
        var model = new ReportsPageViewModel
        {
            AvailableYears = await _surveyReportService.GetAvailableReportYearsAsync(cancellationToken)
        };

        return View("~/Views/Survey/view_reports.cshtml", model);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/monthly/{id:int}")]
    public async Task<IActionResult> CreateMonthlyReport(
        int id,
        int month,
        int year,
        int idOrganization = 0,
        string type = "",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _surveyReportService.CreateSurveyMonthlyReportAsync(
                id,
                idOrganization,
                month,
                year,
                cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return this.SafeError(ex, "Невозможно сформировать отчёт с указанными параметрами.", "Некорректные параметры месячного отчёта по анкете", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сформировать отчёт.", $"Ошибка при формировании месячного отчёта по анкете {id}");
        }
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/monthly")]
    public async Task<IActionResult> CreateMonthlySummaryReport(
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _surveyReportService.CreateAllMonthlyReportAsync(month, year, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return this.SafeError(ex, "Невозможно сформировать отчёт с указанными параметрами.", "Некорректные параметры месячного отчёта", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сформировать отчёт.", "Ошибка при формировании сводного месячного отчёта");
        }
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("reports/quarterly/{quarter}")]
    [HttpGet("reports/quarterly/{quarter}/{year}")]
    public async Task<IActionResult> CreateQuarterlyReport(
        int quarter,
        int year = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _surveyReportService.CreateQuarterlyReportAsync(quarter, year, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return this.SafeError(ex, "Невозможно сформировать отчёт с указанными параметрами.", "Некорректные параметры квартального отчёта", StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось сформировать отчёт.", $"Ошибка при формировании квартального отчёта за {quarter} квартал {year}");
        }
    }
}
