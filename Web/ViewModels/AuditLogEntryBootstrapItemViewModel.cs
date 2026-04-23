namespace MainProject.Web.ViewModels;

public sealed class AuditLogEntryBootstrapItemViewModel
{
    public long Id { get; init; }
    public string Date { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public int? UserId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public string TargetName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ExtraDataJson { get; init; } = string.Empty;
}
