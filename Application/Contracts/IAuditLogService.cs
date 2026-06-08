using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAuditLogService
{
    AuditLogPageViewModel GetLogsPage(int currentPage, int pageSize, string? sortBy, string? sortDirection);
    Log? GetLogDetails(long idLog, int currentPage, int pageSize, string? sortBy, string? sortDirection);
    IReadOnlyList<Log> GetLogs();
    string GenerateLogText(IEnumerable<Log> logs);
}
