namespace MainProject.Application.DTO;

public sealed class DatasetChartViewModel
{
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ChartDatasetViewModel> Datasets { get; init; } = Array.Empty<ChartDatasetViewModel>();
}
