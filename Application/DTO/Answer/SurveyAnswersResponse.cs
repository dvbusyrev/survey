namespace MainProject.Application.DTO;

public sealed class SurveyAnswersResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Details { get; init; }
    public SurveyAnswersSurveyViewModel? Survey { get; init; }
    public IReadOnlyList<SurveyAnswerResultViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerResultViewModel>();
}
