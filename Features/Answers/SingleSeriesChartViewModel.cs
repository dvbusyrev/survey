namespace MainProject.Application.DTO;

public sealed class SingleSeriesChartViewModel
{
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<double> Data { get; init; } = Array.Empty<double>();
}
