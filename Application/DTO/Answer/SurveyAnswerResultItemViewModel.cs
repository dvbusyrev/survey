using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyAnswerResultItemViewModel
{
    [JsonPropertyName("question_text")]
    public string QuestionText { get; init; } = string.Empty;

    public string Rating { get; init; } = "0";

    public string Comment { get; init; } = string.Empty;
}
