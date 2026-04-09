using MainProject.Domain.Entities;

namespace MainProject.Application.Contracts;

public interface IAuditLogService
{
    IReadOnlyList<Log> GetLogs();
    string GenerateLogText(IEnumerable<Log> logs);
}
