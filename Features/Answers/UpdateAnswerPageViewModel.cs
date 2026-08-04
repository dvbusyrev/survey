using MainProject.Application.UseCases.Answers;

namespace MainProject.Web.ViewModels;

public sealed class UpdateAnswerPageViewModel
{
    public int SurveyId { get; init; }
    public int OrganizationId { get; init; }
    public IReadOnlyList<AnswerPayloadItem> Answers { get; init; } = Array.Empty<AnswerPayloadItem>();
}
