using MainProject.Application.DTO;

namespace MainProject.Web.ViewModels;

public sealed class SurveySignaturePageViewModel
{
    public int SurveyId { get; init; }
    public string SurveyName { get; init; } = string.Empty;
    public IReadOnlyList<SurveySignatureStatusViewModel> Items { get; init; } = Array.Empty<SurveySignatureStatusViewModel>();
}
