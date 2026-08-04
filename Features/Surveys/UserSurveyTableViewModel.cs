using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserSurveyTableViewModel
{
    public IReadOnlyList<Survey> Surveys { get; init; } = Array.Empty<Survey>();
    public bool IsArchive { get; init; }
}
