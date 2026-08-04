namespace MainProject.Web.ViewModels;

public sealed class OrganizationSurveyAssignmentsPageViewModel
{
    public IReadOnlyList<OrganizationSurveyGroupViewModel> Organizations { get; init; } =
        Array.Empty<OrganizationSurveyGroupViewModel>();
}

public sealed class OrganizationSurveyGroupViewModel
{
    public int OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public IReadOnlyList<OrganizationSurveyItemViewModel> Surveys { get; init; } =
        Array.Empty<OrganizationSurveyItemViewModel>();
}

public sealed class OrganizationSurveyItemViewModel
{
    public int SurveyId { get; init; }
    public string SurveyName { get; init; } = string.Empty;
    public string BaseEndDateIso { get; init; } = string.Empty;
    public string EffectiveEndDateDisplay { get; init; } = string.Empty;
    public string EffectiveEndDateIso { get; init; } = string.Empty;
    public string RemainingText { get; init; } = string.Empty;
    public bool IsExpired { get; init; }
}
