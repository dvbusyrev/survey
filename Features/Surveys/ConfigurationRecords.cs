namespace MainProject.Application.DTO.Configuration;

public sealed class AutoCreationConfigRecord
{
    public int IdConfig { get; init; }
    public string ReportingPeriod { get; init; } = "month";
    public int ReportingOffsetBusinessDays { get; init; } = 1;
    public int WorkingPeriod { get; init; } = 8;
    public bool IsEnabled { get; init; }
}
