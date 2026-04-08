using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Security;
using MainProject.Models;
using MainProject.Services.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class LogController : Controller
{
    private readonly AuditLogService _auditLogService;

    public LogController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [ActionName("get_logs")]
    public IActionResult GetLogs()
    {
        try
        {
            return View(_auditLogService.GetLogs());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка логов: {ex.Message}" });
        }
    }

    [ActionName("get_dump_logs")]
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
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "dumps", fileName);
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        System.IO.File.WriteAllText(filePath, logText, Encoding.UTF8);

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        return File(fileBytes, "text/plain", fileName);
    }
}
