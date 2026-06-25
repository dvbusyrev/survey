using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;
using MainProject.Application.Support;

[Authorize(Roles = AppRoles.Admin)]
public class LogController : Controller
{
    private const int LogsPageSize = 10;
    private readonly IAuditLogService _auditLogService;
    private readonly IClock _clock;

    public LogController(IAuditLogService auditLogService, IClock clock)
    {
        _auditLogService = auditLogService;
        _clock = clock;
    }

    [HttpGet("logs")]
    [HttpGet("event-log")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View("get_logs", await _auditLogService.GetLogsPageAsync(
                page, LogsPageSize, sortBy, sortDirection, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить журнал событий.", "Ошибка при загрузке журнала событий");
        }
    }

    [HttpGet("logs/export")]
    [HttpGet("event-log/export")]
    public async Task<IActionResult> GetDumpLogs(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Log> logs;

        try
        {
            logs = await _auditLogService.GetLogsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось выгрузить журнал событий.", "Ошибка при получении списка журнала событий");
        }

        var logText = _auditLogService.GenerateLogText(logs);
        var fileName = $"АИС Анкетирование. Журнал событий {_clock.Now:yyyy-MM-dd HH-mm-ss}.txt";
        var fileBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(logText);
        return File(fileBytes, "text/plain", fileName);
    }

    [HttpGet("logs/details/{idLog:long}")]
    [HttpGet("event-log/details/{idLog:long}")]
    public async Task<IActionResult> GetLogDetails(
        long idLog,
        [FromQuery] string? sourceTable = null,
        [FromQuery] int page = 1,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = await _auditLogService.GetLogDetailsAsync(
                idLog, sourceTable, page, LogsPageSize, sortBy, sortDirection, cancellationToken);
            if (log == null)
            {
                return NotFound(new { message = "Событие не найдено" });
            }

            return Json(BuildLogDetailsResponse(log));
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось загрузить событие.", "Ошибка при загрузке события журнала");
        }
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
