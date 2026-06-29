using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Read;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Answers;

public partial class AnswerService
{
    private static readonly string[] ChartBackgroundColors =
    {
        "rgba(79, 70, 229, 0.72)",
        "rgba(14, 165, 233, 0.72)",
        "rgba(16, 185, 129, 0.72)",
        "rgba(245, 158, 11, 0.72)",
        "rgba(239, 68, 68, 0.72)",
        "rgba(168, 85, 247, 0.72)",
        "rgba(20, 184, 166, 0.72)",
        "rgba(244, 114, 182, 0.72)"
    };

    private static readonly string[] ChartBorderColors =
    {
        "rgb(79, 70, 229)",
        "rgb(14, 165, 233)",
        "rgb(16, 185, 129)",
        "rgb(245, 158, 11)",
        "rgb(239, 68, 68)",
        "rgb(168, 85, 247)",
        "rgb(20, 184, 166)",
        "rgb(244, 114, 182)"
    };

    public virtual async Task<AnswerListPageViewModel> GetAnswersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default)
    {
        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var selectedSurveyIds = ParseSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, dateFrom, dateTo);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeAnswerSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeAnswerSortDirection(null, normalizedSortBy);

        var readData = await _answerRepository.GetListAsync(
            new AnswerListReadRequest(
                selectedOrganizationIds,
                selectedSurveyIds,
                bounds.Start,
                bounds.End,
                normalizedSortBy,
                normalizedSortDirection,
                currentPage,
                AppListPaging.DefaultPageSize),
            cancellationToken);

        return new AnswerListPageViewModel
        {
            Answers = readData.Rows.Select(row => new AnswerListItemViewModel
            {
                IdAnswer = row.IdAnswer,
                IdOrganization = row.IdOrganization,
                IdSurvey = row.IdSurvey,
                OrganizationName = row.OrganizationName,
                SurveyName = row.SurveyName,
                CompletionDate = row.CompletionDate,
                IsSigned = row.IsSigned
            }).ToList(),
            CurrentPage = readData.CurrentPage,
            TotalPages = readData.TotalPages,
            TotalCount = readData.TotalCount,
            PageSize = readData.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/surveys/answers",
                EnableDateFilter = true,
                EnableOrganizationFilter = true,
                EnableSurveyFilter = true,
                OrganizationOptions = readData.OrganizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds,
                SurveyOptions = readData.SurveyOptions,
                SelectedSurveyIds = selectedSurveyIds,
                Year = bounds.FilterType == AnswerDateFilterType.Year ? bounds.Year : null,
                Month = bounds.FilterType == AnswerDateFilterType.Month ? bounds.Month : string.Empty,
                DateFrom = bounds.FilterType == AnswerDateFilterType.Range ? bounds.Start?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty,
                DateTo = bounds.FilterType == AnswerDateFilterType.Range ? bounds.End?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty
            }
        };
    }

    public virtual async Task<SurveySignaturePageViewModel> GetSignaturePageAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var readData = await _answerRepository.GetSignatureStatusAsync(surveyId, cancellationToken);
        var items = readData.Rows
            .Select(row => new SurveySignatureStatusViewModel
            {
                OrganizationName = row.OrganizationName ?? string.Empty,
                IsCompleted = row.IsCompleted,
                IsSigned = row.IsSigned,
                CompletionDate = row.CompletionDate
            })
            .ToList();

        return new SurveySignaturePageViewModel
        {
            SurveyId = surveyId,
            SurveyName = readData.SurveyName,
            Items = items
        };
    }

    public virtual async Task<AnswerStatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var readData = await _answerRepository.GetStatisticsAsync(cancellationToken);
        return new AnswerStatisticsResponse
        {
            LineChart = BuildAverageScoreByYearChart(readData.ByYear),
            BarChart = BuildAverageScoreByQuarterChart(readData.ByQuarter),
            AvgScoreByOrganizationRadar = BuildAverageScoreByOrganizationChart(readData.ByOrganization)
        };
    }

    private static SingleSeriesChartViewModel BuildAverageScoreByYearChart(IReadOnlyList<AverageByYearReadRow> rows)
    {
        return new SingleSeriesChartViewModel
        {
            Labels = rows.Select(row => row.Year.ToString()).ToList(),
            Label = "Средняя оценка",
            Data = rows.Select(row => Math.Round(row.AverageRating, 2)).ToList()
        };
    }

    private static SingleSeriesChartViewModel BuildAverageScoreByQuarterChart(IReadOnlyList<AverageByQuarterReadRow> rows)
    {
        var averagesByQuarter = rows
            .ToDictionary(row => row.Quarter, row => Math.Round(row.AverageRating, 2));

        return new SingleSeriesChartViewModel
        {
            Labels = Enumerable.Range(1, 4).Select(quarter => quarter.ToString()).ToList(),
            Label = "Средняя оценка",
            Data = Enumerable.Range(1, 4)
                .Select(quarter => averagesByQuarter.TryGetValue(quarter, out var value) ? value : 0)
                .ToList()
        };
    }

    private static DatasetChartViewModel BuildAverageScoreByOrganizationChart(IReadOnlyList<OrganizationAverageReadRow> rows)
    {
        if (rows.Count == 0)
        {
            return new DatasetChartViewModel();
        }

        var labels = rows
            .Select(row => row.OrganizationName ?? "Неизвестно")
            .ToList();

        var datasets = rows
            .Select((row, index) => new ChartDatasetViewModel
            {
                Label = row.OrganizationName ?? "Неизвестно",
                Data = labels
                    .Select((_, dataIndex) => dataIndex == index ? Math.Round(row.AverageRating, 2) : (double?)null)
                    .ToList(),
                BackgroundColor = ChartBackgroundColors[index % ChartBackgroundColors.Length],
                BorderColor = ChartBorderColors[index % ChartBorderColors.Length]
            })
            .ToList();

        return new DatasetChartViewModel
        {
            Labels = labels,
            Datasets = datasets
        };
    }

    private static IReadOnlyList<int> ParseSelectedIds(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<int>()
            : rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
    }

    private static (AnswerDateFilterType FilterType, int? Year, string Month, DateTime? Start, DateTime? End) ResolveDateBounds(
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo)
    {
        if (int.TryParse(year, out var parsedYear) && parsedYear >= 1900 && parsedYear <= 3000)
        {
            return (
                AnswerDateFilterType.Year,
                parsedYear,
                string.Empty,
                new DateTime(parsedYear, 1, 1),
                new DateTime(parsedYear, 12, 31, 23, 59, 59));
        }

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(
                $"{month}-01",
                "yyyy-MM-dd",
                null,
                System.Globalization.DateTimeStyles.None,
                out var parsedMonth))
        {
            var monthEndDay = DateTime.DaysInMonth(parsedMonth.Year, parsedMonth.Month);
            return (
                AnswerDateFilterType.Month,
                null,
                month.Trim(),
                new DateTime(parsedMonth.Year, parsedMonth.Month, 1),
                new DateTime(parsedMonth.Year, parsedMonth.Month, monthEndDay, 23, 59, 59));
        }

        if (DateTime.TryParse(dateFrom, out var parsedDateFrom)
            && DateTime.TryParse(dateTo, out var parsedDateTo))
        {
            return (
                AnswerDateFilterType.Range,
                null,
                string.Empty,
                parsedDateFrom.Date,
                parsedDateTo.Date.AddDays(1).AddTicks(-1));
        }

        return (AnswerDateFilterType.None, null, string.Empty, null, null);
    }

    private static string NormalizeAnswerSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            AnswerReadSortFields.Organization => AnswerReadSortFields.Organization,
            AnswerReadSortFields.Survey => AnswerReadSortFields.Survey,
            AnswerReadSortFields.Signed => AnswerReadSortFields.Signed,
            _ => AnswerReadSortFields.Date
        };
    }

    private static string NormalizeAnswerSortDirection(string? sortDirection, string sortField)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return sortField switch
        {
            AnswerReadSortFields.Date => "desc",
            AnswerReadSortFields.Signed => "desc",
            _ => "asc"
        };
    }

    private enum AnswerDateFilterType
    {
        None,
        Year,
        Month,
        Range
    }

}
