namespace MainProject.Application.DTO;

public sealed class ChartDatasetViewModel
{
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<double> Data { get; init; } = Array.Empty<double>();
    public string? BackgroundColor { get; init; }
    public string? BorderColor { get; init; }
}
