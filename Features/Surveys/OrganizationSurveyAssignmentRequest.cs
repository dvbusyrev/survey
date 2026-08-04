using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class OrganizationSurveyAssignmentRequest
{
    [JsonPropertyName("organizationId")]
    public int OrganizationId { get; init; }

    [JsonPropertyName("surveyId")]
    public int SurveyId { get; init; }
}
