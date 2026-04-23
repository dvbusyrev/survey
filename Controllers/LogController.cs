using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Security;
using MainProject.Web.Formatting;
using MainProject.Web.ViewModels;
using Newtonsoft.Json;

[Authorize(Roles = AppRoles.Admin)]
public class LogController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public LogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet("logs")]
    public IActionResult GetLogs()
    {
        try
        {
            return View("get_logs", BuildLogsPageViewModel(_auditLogService.GetLogs()));
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка логов: {ex.Message}" });
        }
    }

    [HttpGet("logs/export")]
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
        var fileName = $"logs_dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var fileBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(logText);
        return File(fileBytes, "text/plain", fileName);
    }

    private static AuditLogPageViewModel BuildLogsPageViewModel(IReadOnlyList<Log> logs)
    {
        var safeLogs = logs ?? Array.Empty<Log>();
        var bootstrapItems = safeLogs.Select(log => new AuditLogEntryBootstrapItemViewModel
        {
            Id = log.IdLog,
            Date = log.Date.ToString("dd.MM.yyyy HH:mm:ss"),
            User = string.IsNullOrWhiteSpace(log.NameUser) ? "Система" : log.NameUser,
            UserId = log.IdUser,
            EventType = string.IsNullOrWhiteSpace(log.EventType) ? "—" : log.EventType,
            TargetType = string.IsNullOrWhiteSpace(log.TargetType) ? "—" : log.TargetType,
            TargetName = string.IsNullOrWhiteSpace(log.TargetName) ? log.TargetType ?? "—" : log.TargetName,
            Description = string.IsNullOrWhiteSpace(log.Description) ? "—" : log.Description,
            ExtraDataJson = LogEntryJsonFormatter.Format(log.ExtraData)
        }).ToList();

        return new AuditLogPageViewModel
        {
            Logs = safeLogs,
            LogsBootstrapJson = JsonConvert.SerializeObject(bootstrapItems)
        };
    }
}
