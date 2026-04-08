using System.Text.Json.Serialization;

namespace MainProject.Services.Admin;

public sealed class SurveyExtensionRequest
{
    [JsonPropertyName("surveyId")]
    public int SurveyId { get; init; }

    [JsonPropertyName("extensions")]
    public List<SurveyExtensionItemRequest> Extensions { get; init; } = new();
}

public sealed class SurveyExtensionItemRequest
{
    [JsonPropertyName("organizationId")]
    public int OrganizationId { get; init; }

    [JsonPropertyName("extendedUntil")]
    public string ExtendedUntil { get; init; } = string.Empty;
}
