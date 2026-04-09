using System.Text.Json.Serialization;

namespace MainProject.Application.DTO;

public sealed class SurveyAnswersSurveyViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    [JsonPropertyName("is_archive")]
    public bool IsArchive { get; init; }

    public string? Csp { get; init; }
}
