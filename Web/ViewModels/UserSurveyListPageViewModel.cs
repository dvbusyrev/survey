using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserSurveyListPageViewModel
{
    public IReadOnlyList<Survey> AccessibleSurveys { get; init; } = Array.Empty<Survey>();
    public int UserOrganizationId { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public string SearchTerm { get; init; } = string.Empty;
}
