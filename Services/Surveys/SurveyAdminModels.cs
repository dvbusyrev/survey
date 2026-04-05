using main_project.Models;

namespace main_project.Services.Surveys;

public sealed class SurveyAddRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public List<int> Organizations { get; set; } = new();
    public List<string> Criteria { get; set; } = new();
}

public sealed class SurveyUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<int> Organizations { get; set; } = new();
    public List<string> Criteria { get; set; } = new();
}

public sealed class SurveyCopyRequest
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public sealed class DeleteSurveyRequest
{
    public int SurveyId { get; set; }
}

public sealed class OmsuSelectionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SurveyEditPageViewModel
{
    public Survey Survey { get; init; } = new();
    public IReadOnlyList<OmsuSelectionItem> AllOmsu { get; init; } = Array.Empty<OmsuSelectionItem>();
    public IReadOnlyList<int> SelectedOmsuIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> SelectedOmsuNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Criteria { get; init; } = Array.Empty<string>();
}
