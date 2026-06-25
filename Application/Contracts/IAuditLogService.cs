using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAuditLogService
{
    Task<AuditLogPageViewModel> GetLogsPageAsync(int currentPage, int pageSize, string? sortBy, string? sortDirection, CancellationToken cancellationToken = default);
    Task<Log?> GetLogDetailsAsync(long idLog, string? sourceTable, int currentPage, int pageSize, string? sortBy, string? sortDirection, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Log>> GetLogsAsync(CancellationToken cancellationToken = default);
    string GenerateLogText(IEnumerable<Log> logs);
}
