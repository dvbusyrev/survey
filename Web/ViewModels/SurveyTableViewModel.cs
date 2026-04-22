namespace MainProject.Web.ViewModels;

public sealed class SurveyTableViewModel
{
    public IReadOnlyList<SurveyTableRowViewModel> Surveys { get; init; } = Array.Empty<SurveyTableRowViewModel>();
    public bool IsArchive { get; init; }
}
