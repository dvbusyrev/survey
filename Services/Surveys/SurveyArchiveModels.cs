using Newtonsoft.Json;
using main_project.Models;

namespace main_project.Services.Surveys;

public sealed class UserSurveyArchivePageViewModel
{
    public IReadOnlyList<Survey> ArchivedSurveys { get; init; } = Array.Empty<Survey>();
    public int UserOmsuId { get; init; }
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
    [JsonProperty("name_survey")]
    public string NameSurvey { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("questions")]
    public string? Questions { get; set; }

    [JsonProperty("date_open")]
    public string? DateOpen { get; set; }

    [JsonProperty("date_close")]
    public string? DateClose { get; set; }

    [JsonProperty("name_omsu")]
    public string? NameOmsu { get; set; }

    [JsonProperty("id_omsu")]
    public int IdOmsu { get; set; }

    [JsonProperty("csp")]
    public string? Csp { get; set; }

    [JsonProperty("id_answer")]
    public int IdAnswer { get; set; }
}
