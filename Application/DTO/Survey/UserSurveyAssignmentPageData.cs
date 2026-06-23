using MainProject.Domain.Entities;

namespace MainProject.Application.DTO;

public sealed class UserSurveyAssignmentPageData
{
    public int TotalCount { get; init; }
    public IReadOnlyList<Survey> Surveys { get; init; } = Array.Empty<Survey>();
}
