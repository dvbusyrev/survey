using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class OrganizationSaveRequest
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Email")]
    public string? Email { get; init; }

    [JsonPropertyName("DateBegin")]
    public string? DateBegin { get; init; }

    [JsonPropertyName("DateEnd")]
    public string? DateEnd { get; init; }
}
