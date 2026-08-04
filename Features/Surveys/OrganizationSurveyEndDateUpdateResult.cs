namespace MainProject.Application.DTO;

public sealed class OrganizationSurveyEndDateUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<OrganizationSurveyAssignmentUpdateItem> UpdatedAssignments { get; init; } =
        Array.Empty<OrganizationSurveyAssignmentUpdateItem>();
}
