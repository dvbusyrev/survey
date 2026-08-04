namespace MainProject.Application.DTO;

public sealed class SurveyUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<int> Organizations { get; set; } = new();
    public List<string> Criteria { get; set; } = new();
}
