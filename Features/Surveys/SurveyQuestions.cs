using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyQuestions
{
    [JsonPropertyName("questions")]
    public SurveyQuestion[]? Questions { get; set; }

    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }
}
