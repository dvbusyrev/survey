using Newtonsoft.Json;

namespace main_project.Services.Answers;

public sealed class AnswerPayloadItem
{
    [JsonProperty("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonProperty("question_text")]
    public string? QuestionText { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("rating")]
    public int? Rating { get; set; }

    [JsonProperty("comment")]
    public string? Comment { get; set; }

    public string DisplayQuestion => QuestionText ?? Text ?? string.Empty;
    public string DisplayRating => Rating?.ToString() ?? "0";
}
