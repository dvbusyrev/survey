using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyAnswerResultViewModel
{
    public int Id { get; init; }

    [JsonPropertyName("organization_id")]
    public int OrganizationId { get; init; }

    [JsonPropertyName("organization_name")]
    public string OrganizationName { get; init; } = string.Empty;

    public string Date { get; init; } = string.Empty;
    public IReadOnlyList<SurveyAnswerResultItemViewModel> Answers { get; init; } = Array.Empty<SurveyAnswerResultItemViewModel>();

    [JsonPropertyName("is_signed")]
    public bool IsSigned { get; init; }

    public string? Signature { get; init; }
}
