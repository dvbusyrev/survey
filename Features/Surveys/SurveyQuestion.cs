using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyQuestion
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("question_id")]
    public int QuestionId { get; set; }
}
