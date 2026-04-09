namespace MainProject.Application.DTO;

public sealed class SurveyAnswerEntryViewModel
{
    public int IdAnswer { get; init; }
    public int IdOrganization { get; init; }
    public int IdSurvey { get; init; }
    public string NameOrganization { get; init; } = string.Empty;
    public string? Csp { get; init; }
    public DateTime? CompletionDate { get; init; }
    public IReadOnlyList<SurveyAnswerDetailViewModel> Details { get; init; } = Array.Empty<SurveyAnswerDetailViewModel>();
}
