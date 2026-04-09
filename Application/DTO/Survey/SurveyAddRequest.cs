namespace MainProject.Application.DTO;

public sealed class SurveyAddRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public List<int> Organizations { get; set; } = new();
    public List<string> Criteria { get; set; } = new();
}
