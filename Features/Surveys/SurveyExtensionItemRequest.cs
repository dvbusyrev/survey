using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyExtensionItemRequest
{
    [JsonPropertyName("organizationId")]
    public int OrganizationId { get; init; }

    [JsonPropertyName("extendedUntil")]
    public string ExtendedUntil { get; init; } = string.Empty;
}
