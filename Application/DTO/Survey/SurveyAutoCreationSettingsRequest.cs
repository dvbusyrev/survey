namespace MainProject.Application.DTO;

public sealed class SurveyAutoCreationSettingsRequest
{
    public string CreationPattern { get; set; } = string.Empty;
    public string StartPattern { get; set; } = string.Empty;
    public int? EndOffsetBusinessDays { get; set; }
    public List<int> SurveyIds { get; set; } = new();
}
