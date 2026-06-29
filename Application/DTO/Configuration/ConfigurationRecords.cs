namespace MainProject.Application.DTO.Configuration;

public sealed class AutoCreationConfigRecord
{
    public int IdConfig { get; init; }
    public int CreationDayId { get; init; }
    public int BeginDayId { get; init; }
    public int? WorkingPeriod { get; init; }
    public string CreationPattern { get; init; } = "1-monday";
    public string StartPattern { get; init; } = "1-monday";
    public string CreationDayName { get; init; } = "Monday";
    public int CreationWeekNumber { get; init; } = 1;
    public string BeginDayName { get; init; } = "Monday";
    public int BeginWeekNumber { get; init; } = 1;
    public bool IsEnabled { get; init; }
}
