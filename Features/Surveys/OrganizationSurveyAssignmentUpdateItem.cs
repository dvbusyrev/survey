namespace MainProject.Application.DTO;

public sealed class OrganizationSurveyAssignmentUpdateItem
{
    public int OrganizationId { get; init; }
    public int SurveyId { get; init; }
    public string EffectiveEndDateDisplay { get; init; } = string.Empty;
    public string EffectiveEndDateIso { get; init; } = string.Empty;
    public string RemainingText { get; init; } = string.Empty;
    public bool IsExpired { get; init; }
}
