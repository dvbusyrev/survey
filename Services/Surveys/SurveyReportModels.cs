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
    [JsonPropertyName("questions")]
    public SurveyQuestion[]? Questions { get; set; }

    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }
}

public sealed class SurveyQuestion
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("question_id")]
    public int QuestionId { get; set; }
}

public sealed class AnswerData
{
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("question_id")]
    public string? QuestionId { get; set; }
}
