using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class AnswerData
{
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("question_id")]
    public string? QuestionId { get; set; }
}
