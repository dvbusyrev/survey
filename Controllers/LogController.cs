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

    [HttpGet("event-log/details/{idLog:long}")]
    public IActionResult GetLogDetails(
        long idLog,
        [FromQuery] int page = 1,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        try
        {
            var log = _auditLogService.GetLogDetails(idLog, page, LogsPageSize, sortBy, sortDirection);
            if (log == null)
            {
                return NotFound(new { message = "Событие не найдено" });
            }

            return Json(BuildLogDetailsResponse(log));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Не удалось загрузить событие: {ex.Message}" });
        }
    }

    [HttpGet("logs/export")]
    public IActionResult RedirectLegacyDumpLogs()
    {
        return RedirectPermanent("/event-log/export");
    }

    private static object BuildLogDetailsResponse(Log log)
    {
        return new
        {
            id = log.IdLog,
            date = log.Date.ToString("dd.MM.yyyy HH:mm:ss"),
            user = string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser,
            eventType = string.IsNullOrWhiteSpace(log.EventType) ? "—" : log.EventType,
            targetType = log.TargetType ?? string.Empty,
            targetName = log.TargetName ?? string.Empty,
            description = log.Description ?? string.Empty,
            extraDataJson = log.ExtraData switch
            {
                Newtonsoft.Json.Linq.JToken token => token.ToString(Newtonsoft.Json.Formatting.None),
                null => string.Empty,
                _ => log.ExtraData.ToString() ?? string.Empty
            }
        };
    }
}
