namespace MainProject.Application.DTO.Organization;

public sealed record OrganizationWriteModel(
    string Name,
    string? ShortName,
    string? Email,
    DateTime? DateBegin,
    DateTime? DateEnd);

public sealed class OrganizationSurveyAssignmentRecord
{
    public int OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public int? SurveyId { get; init; }
    public string? SurveyName { get; init; }
    public DateTime? AssignmentDateEnd { get; init; }
}

public sealed record OrganizationArchiveResult(
    bool Found,
    bool Archived,
    IReadOnlyList<string> SurveyNames,
    IReadOnlyList<string> UserNames);
