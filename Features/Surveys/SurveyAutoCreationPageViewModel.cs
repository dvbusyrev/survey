namespace MainProject.Web.ViewModels;

public sealed class SurveyAutoCreationPageViewModel
{
    public string ReportingPeriod { get; init; } = "month";
    public int ReportingOffsetBusinessDays { get; init; } = 1;
    public int ActivePeriodBusinessDays { get; init; } = 8;
    public int PreviewYear { get; init; }
    public int PreviewMonth { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<SurveyAutoCreationSelectedTemplateViewModel> SelectedTemplates { get; init; }
        = Array.Empty<SurveyAutoCreationSelectedTemplateViewModel>();
}

public sealed class SurveyAutoCreationSelectedTemplateViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
