using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MainProject.Services.Surveys;

public sealed class GeneratedFileResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = string.Empty;
}

public sealed class SurveyQuestions
{
    [JsonProperty("questions")]
    [JsonPropertyName("questions")]
    public SurveyQuestion[]? Questions { get; set; }

    [JsonProperty("survey_id")]
    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }
}

public sealed class SurveyQuestion
{
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonProperty("question_id")]
    [JsonPropertyName("question_id")]
    public int QuestionId { get; set; }
}

public sealed class AnswerData
{
    [JsonProperty("rating")]
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonProperty("comment")]
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonProperty("question_id")]
    [JsonPropertyName("question_id")]
    public string? QuestionId { get; set; }
}
