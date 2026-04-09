using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class ArchiveSurveyCopyRequest
{
    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }
}
