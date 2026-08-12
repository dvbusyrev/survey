namespace MainProject.Application.DTO;

public sealed class SurveyAssignmentPeriodState
{
    public int AssignmentId { get; init; }
    public DateTime AssignmentDateBegin { get; init; }
    public DateTime? AssignmentDateEnd { get; init; }
    public DateTime BaseDateBegin { get; init; }
    public DateTime? BaseDateEnd { get; init; }
    public bool HasAnswer { get; init; }
}
