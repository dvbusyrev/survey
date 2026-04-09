using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyAnswerDetailViewModel
{
    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    public string DisplayQuestion => QuestionText ?? Text ?? string.Empty;
}
