namespace MainProject.Application.DTO.Audit;

public sealed class AuditLogRow
{
    public string SourceTable { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
    public long IdAudit { get; init; }
    public long? ParentAuditId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
    public int? ChangedByUserId { get; init; }
    public string? ActorName { get; init; }
    public string? TargetName { get; init; }
    public string? TargetId { get; init; }
    public string? RelatedKind { get; init; }
    public string? RelatedId { get; init; }
    public string? RecordPkJson { get; init; }
    public string? RowDataJson { get; init; }
    public string? OldRowDataJson { get; init; }
    public string? NewRowDataJson { get; init; }
}

public sealed record AuditLogReadResult(
    IReadOnlyList<AuditLogRow> Rows,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SourceColumnOrders);

public sealed class AuditAnswerContext
{
    public int IdSurvey { get; init; }
    public string? SurveyName { get; init; }
    public int IdOrganization { get; init; }
    public string? OrganizationName { get; init; }
}
