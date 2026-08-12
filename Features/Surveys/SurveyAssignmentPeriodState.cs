namespace MainProject.Application.DTO;

public sealed class SurveyAssignmentPeriodState
{
    public DateTime AssignmentDateBegin { get; init; }
    public DateTime? AssignmentDateEnd { get; init; }
    public DateTime BaseDateBegin { get; init; }
    public DateTime? BaseDateEnd { get; init; }
}
