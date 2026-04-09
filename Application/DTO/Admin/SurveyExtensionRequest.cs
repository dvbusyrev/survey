using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyExtensionRequest
{
    [JsonPropertyName("surveyId")]
    public int SurveyId { get; init; }

    [JsonPropertyName("extensions")]
    public List<SurveyExtensionItemRequest> Extensions { get; init; } = new();
}
