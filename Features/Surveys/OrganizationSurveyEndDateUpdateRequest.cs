using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class OrganizationSurveyEndDateUpdateRequest
{
    [JsonPropertyName("dateEnd")]
    public string DateEnd { get; init; } = string.Empty;

    [JsonPropertyName("assignments")]
    public List<OrganizationSurveyAssignmentRequest> Assignments { get; init; } = new();
}
