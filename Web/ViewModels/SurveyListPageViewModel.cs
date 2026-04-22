using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class SurveyListPageViewModel
{
    public IReadOnlyList<Survey> Surveys { get; init; } = Array.Empty<Survey>();
    public bool OpenAddSurveyModal { get; init; }
    public SurveyEditPageViewModel? EditSurveyPage { get; init; }
}
