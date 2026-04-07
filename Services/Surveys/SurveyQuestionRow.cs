namespace main_project.Services.Surveys;

internal sealed class SurveyQuestionRow
{
    public int QuestionOrder { get; init; }
    public string QuestionText { get; init; } = string.Empty;
}
