namespace MainProject.Application.DTO;

public sealed class SurveyAssignmentTableRow
{
    public int IdSurvey { get; init; }
    public string? NameSurvey { get; init; }
    public string? OriginalNameSurvey { get; init; }
    public DateTime DateBegin { get; init; }
    public DateTime? DateEnd { get; init; }
    public int[]? OrganizationIds { get; init; }
    public string[]? OrganizationNames { get; init; }
    public bool IsExtension { get; init; }
    public int? ExtensionOrganizationId { get; init; }
}
