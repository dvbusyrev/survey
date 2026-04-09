using System.Text.Json.Serialization;

namespace MainProject.Services.Answers;

public sealed class AnswerPayloadItem
{
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    public string DisplayQuestion => QuestionText ?? Text ?? string.Empty;
    public string DisplayRating => Rating?.ToString() ?? "0";
}
