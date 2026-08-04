namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyQuestionRow
{
    public int QuestionOrder { get; init; }
    public string QuestionText { get; init; } = string.Empty;
}
