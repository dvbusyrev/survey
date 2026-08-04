namespace MainProject.Application.DTO;

public sealed class AnswerStatisticsResponse
{
    public SingleSeriesChartViewModel LineChart { get; init; } = new();
    public SingleSeriesChartViewModel PieChart { get; init; } = new();
    public SingleSeriesChartViewModel BarChart { get; init; } = new();
    public DatasetChartViewModel RadarChart { get; init; } = new();
    public DatasetChartViewModel AvgScoreByOrganizationRadar { get; init; } = new();
}
