namespace MainProject.Application.DTO;

public sealed class SurveyCopyTemplateResponse
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
    public IReadOnlyList<OrganizationSelectionItem> Organizations { get; init; } = Array.Empty<OrganizationSelectionItem>();
    public IReadOnlyList<string> Criteria { get; init; } = Array.Empty<string>();
    public bool IsAutoCreationEnabled { get; init; }
}
