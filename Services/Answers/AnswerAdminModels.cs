namespace main_project.Services.Answers;

public sealed class AnswerListPageViewModel
{
    public IReadOnlyList<AnswerListItemViewModel> Answers { get; init; } = Array.Empty<AnswerListItemViewModel>();
}

public sealed class AnswerListItemViewModel
{
    public int IdAnswer { get; init; }
    public int IdOmsu { get; init; }
    public int IdSurvey { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public string SurveyName { get; init; } = string.Empty;
    public DateTime? CompletionDate { get; init; }
    public bool IsSigned { get; init; }
}

public sealed class SurveySignaturePageViewModel
{
    public int SurveyId { get; init; }
    public string SurveyName { get; init; } = string.Empty;
    public IReadOnlyList<SurveySignatureStatusViewModel> Items { get; init; } = Array.Empty<SurveySignatureStatusViewModel>();
}

public sealed class SurveySignatureStatusViewModel
{
    public string OrganizationName { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public bool IsSigned { get; init; }
    public string CompletionStatus => IsCompleted ? "Пройдена" : "Не пройдена";
    public string SignatureStatus => IsSigned ? "Подписана" : "Не подписана";
}

public sealed class AnswerStatisticsResponse
{
    public SingleSeriesChartViewModel LineChart { get; init; } = new();
    public SingleSeriesChartViewModel PieChart { get; init; } = new();
    public SingleSeriesChartViewModel BarChart { get; init; } = new();
    public DatasetChartViewModel RadarChart { get; init; } = new();
    public DatasetChartViewModel AvgScoreByOmsuRadar { get; init; } = new();
}

public sealed class SingleSeriesChartViewModel
{
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<double> Data { get; init; } = Array.Empty<double>();
}

public sealed class DatasetChartViewModel
{
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ChartDatasetViewModel> Datasets { get; init; } = Array.Empty<ChartDatasetViewModel>();
}

public sealed class ChartDatasetViewModel
{
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<double> Data { get; init; } = Array.Empty<double>();
    public string? BackgroundColor { get; init; }
    public string? BorderColor { get; init; }
}
