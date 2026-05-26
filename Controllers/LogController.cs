using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Security;
using MainProject.Web.ViewModels;
using MainProject.Application.Support;

[Authorize(Roles = AppRoles.Admin)]
public class LogController : Controller
{
    private const int LogsPageSize = 10;
    private readonly IAuditLogService _auditLogService;

    public LogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet("event-log")]
    public IActionResult GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        try
        {
            return View("get_logs", _auditLogService.GetLogsPage(page, LogsPageSize, sortBy, sortDirection));
        }
        catch (Exception ex)
        {
            ViewData["LogLoadErrorMessage"] = $"Не удалось загрузить журнал событий: {ex.Message}";
            return View("get_logs", new AuditLogPageViewModel
            {
                HasExplicitSort = AppSortState.HasExplicitSort(sortBy),
                CurrentPage = 1,
                TotalPages = 1,
                TotalCount = 0,
                PageSize = LogsPageSize,
                SortBy = AppSortState.HasExplicitSort(sortBy) ? sortBy ?? string.Empty : string.Empty,
                SortDirection = AppSortState.HasExplicitSort(sortBy) ? AppSortState.NormalizeExplicitDirection(sortDirection) : string.Empty
            });
        }
    }

    [HttpGet("logs")]
    public IActionResult RedirectLegacyLogs()
    {
        return RedirectPermanent("/event-log");
    }

    [HttpGet("event-log/export")]
    public IActionResult GetDumpLogs()
    {
        IReadOnlyList<Log> logs;

        try
        {
            logs = _auditLogService.GetLogs();
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка логов: {ex.Message}" });
        }

        var logText = _auditLogService.GenerateLogText(logs);
        var fileName = $"АИС Анкетирование. Журнал событий {DateTime.Now:yyyy-MM-dd HH-mm-ss}.txt";
        var fileBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(logText);
        return File(fileBytes, "text/plain", fileName);
    }

    [HttpGet("logs/export")]
    public IActionResult RedirectLegacyDumpLogs()
    {
        return RedirectPermanent("/event-log/export");
    }
}
