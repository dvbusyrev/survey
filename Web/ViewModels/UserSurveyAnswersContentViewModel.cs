using MainProject.Application.DTO;

namespace MainProject.Web.ViewModels;

public sealed class UserSurveyAnswersContentViewModel
{
    public SurveyAnswersSurveyViewModel Survey { get; init; } = new();
    public int OrganizationId { get; init; }
    public IReadOnlyList<SurveyAnswerResultViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerResultViewModel>();
}
