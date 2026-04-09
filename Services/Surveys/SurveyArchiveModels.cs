using MainProject.Models;
using System.Text.Json.Serialization;

namespace MainProject.Services.Surveys;

public sealed class UserSurveyArchivePageViewModel
{
    public IReadOnlyList<Survey> ArchivedSurveys { get; init; } = Array.Empty<Survey>();
    public int UserOrganizationId { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public string SearchTerm { get; init; } = string.Empty;
    public string DateFrom { get; init; } = string.Empty;
    public string DateTo { get; init; } = string.Empty;
    public bool SignedOnly { get; init; }
}

public sealed class ArchiveSurveyCopyRequest
{
    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }
}
