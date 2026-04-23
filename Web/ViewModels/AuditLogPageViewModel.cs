using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class AuditLogPageViewModel
{
    public IReadOnlyList<Log> Logs { get; init; } = Array.Empty<Log>();
    public string LogsBootstrapJson { get; init; } = "[]";
}
