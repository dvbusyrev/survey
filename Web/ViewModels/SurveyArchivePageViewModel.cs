using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class SurveyArchivePageViewModel
{
    public IReadOnlyList<ArchivedSurvey> Surveys { get; init; } = Array.Empty<ArchivedSurvey>();
    public SurveyEditPageViewModel? EditSurveyPage { get; init; }
}
