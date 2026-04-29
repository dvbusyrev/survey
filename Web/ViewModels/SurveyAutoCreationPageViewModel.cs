namespace MainProject.Web.ViewModels;

public sealed class SurveyAutoCreationPageViewModel
{
    public string CreationPattern { get; init; } = "1-monday";
    public string StartPattern { get; init; } = "1-monday";
    public int EndOffsetBusinessDays { get; init; } = 8;
    public bool IsEnabled { get; init; }
    public IReadOnlyList<SurveyAutoCreationSelectedSurveyViewModel> SelectedSurveys { get; init; }
        = Array.Empty<SurveyAutoCreationSelectedSurveyViewModel>();
}

public sealed class SurveyAutoCreationSelectedSurveyViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
