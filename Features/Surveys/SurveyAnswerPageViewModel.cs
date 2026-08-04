using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class SurveyAnswerPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<SurveyAnswerEntryViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerEntryViewModel>();
    public string Role { get; init; } = string.Empty;
}
