using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Services.Answers;

public sealed class AnswerPayloadItem
{
    [JsonProperty("question_id")]
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonProperty("question_text")]
    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }

    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonProperty("rating")]
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonProperty("comment")]
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    public string DisplayQuestion => QuestionText ?? Text ?? string.Empty;
    public string DisplayRating => Rating?.ToString() ?? "0";
}
