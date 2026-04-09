namespace MainProject.Application.DTO;

public sealed class AnswerListItemViewModel
{
    public int IdAnswer { get; init; }
    public int IdOrganization { get; init; }
    public int IdSurvey { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public string SurveyName { get; init; } = string.Empty;
    public DateTime? CompletionDate { get; init; }
    public bool IsSigned { get; init; }
}
