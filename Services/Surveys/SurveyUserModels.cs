using main_project.Models;

namespace main_project.Services.Surveys;

public sealed class UserSurveyListPageViewModel
{
    public IReadOnlyList<Survey> AccessibleSurveys { get; init; } = Array.Empty<Survey>();
    public int UserOmsuId { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public string SearchTerm { get; init; } = string.Empty;
}

public sealed class SurveyQuestionItem
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
}
