using MainProject.Application.DTO;

namespace MainProject.Web.ViewModels;

public sealed class AnswerListPageViewModel
{
    public IReadOnlyList<AnswerListItemViewModel> Answers { get; init; } = Array.Empty<AnswerListItemViewModel>();
}
