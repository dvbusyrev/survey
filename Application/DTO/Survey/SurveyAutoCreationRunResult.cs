namespace MainProject.Application.DTO;

public sealed class SurveyAutoCreationRunResult
{
    public bool IsEnabled { get; init; }
    public bool WasDue { get; init; }
    public bool Processed { get; init; }
    public int CreatedSurveyCount { get; init; }
    public DateTime? ScheduleDate { get; init; }
}
