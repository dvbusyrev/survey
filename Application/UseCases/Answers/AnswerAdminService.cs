using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerAdminService : IAnswerAdminService
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

    private readonly IDbConnectionFactory _connectionFactory;

    public AnswerAdminService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public AnswerListPageViewModel GetAnswersPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                ha.id_answer AS IdAnswer,
                os.id_organization AS IdOrganization,
                os.id_survey AS IdSurvey,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, 'Нет данных') AS OrganizationName,
                COALESCE(s.name_survey, 'Нет данных') AS SurveyName,
                ha.completion_date AS CompletionDate,
                COALESCE(ha.csp, '') AS Signature
            FROM public.answer ha
            INNER JOIN public.organization_survey os
                ON os.id_organization_survey = ha.id_organization_survey
            LEFT JOIN public.organization o
                ON o.id_organization = os.id_organization
            LEFT JOIN public.survey s
                ON s.id_survey = os.id_survey
            ORDER BY ha.completion_date DESC NULLS LAST, ha.id_answer DESC";

        var rows = connection.Query<AnswerListRow>(sql).ToList();
        var organizationOptions = rows
            .Where(row => row.IdOrganization > 0 && !string.IsNullOrWhiteSpace(row.OrganizationName))
            .GroupBy(row => row.IdOrganization)
            .Select(group => new SelectionOption
            {
                Id = group.Key,
                Name = group.First().OrganizationName!.Trim()
            })
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
        var surveyOptions = rows
            .Where(row => row.IdSurvey > 0 && !string.IsNullOrWhiteSpace(row.SurveyName))
            .GroupBy(row => row.IdSurvey)
            .Select(group => new SelectionOption
            {
                Id = group.Key,
                Name = group.First().SurveyName!.Trim()
            })
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();

        var selectedOrganizationIds = ParseSelectedIds(organizationIds);
        var selectedSurveyIds = ParseSelectedIds(surveyIds);
        var bounds = ResolveDateBounds(year, month, dateFrom, dateTo);
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeAnswerSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeAnswerSortDirection(null, normalizedSortBy);

        var filteredRows = rows
            .Where(row => selectedOrganizationIds.Count == 0 || selectedOrganizationIds.Contains(row.IdOrganization))
            .Where(row => selectedSurveyIds.Count == 0 || selectedSurveyIds.Contains(row.IdSurvey))
            .Where(row => MatchesDateBounds(row.CompletionDate, bounds.Start, bounds.End))
            .Select(row => new AnswerListItemViewModel
            {
                IdAnswer = row.IdAnswer,
                IdOrganization = row.IdOrganization,
                IdSurvey = row.IdSurvey,
                OrganizationName = row.OrganizationName ?? "Нет данных",
                SurveyName = row.SurveyName ?? "Нет данных",
                CompletionDate = row.CompletionDate,
                IsSigned = !string.IsNullOrWhiteSpace(row.Signature)
            })
            .ToList();

        var sortedRows = SortAnswerRows(filteredRows, normalizedSortBy, normalizedSortDirection);
        var pageSlice = AppListPaging.Slice(sortedRows, currentPage);

        return new AnswerListPageViewModel
        {
            Answers = pageSlice.Items,
            CurrentPage = pageSlice.CurrentPage,
            TotalPages = pageSlice.TotalPages,
            TotalCount = pageSlice.TotalCount,
            PageSize = pageSlice.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            FilterState = new ServerTableFilterStateViewModel
            {
                BasePath = "/surveys/answers",
                EnableDateFilter = true,
                EnableOrganizationFilter = true,
                EnableSurveyFilter = true,
                OrganizationOptions = organizationOptions,
                SelectedOrganizationIds = selectedOrganizationIds,
                SurveyOptions = surveyOptions,
                SelectedSurveyIds = selectedSurveyIds,
                Year = bounds.FilterType == AnswerDateFilterType.Year ? bounds.Year : null,
                Month = bounds.FilterType == AnswerDateFilterType.Month ? bounds.Month : string.Empty,
                DateFrom = bounds.FilterType == AnswerDateFilterType.Range ? bounds.Start?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty,
                DateTo = bounds.FilterType == AnswerDateFilterType.Range ? bounds.End?.ToString("yyyy-MM-dd") ?? string.Empty : string.Empty
            }
        };
    }

    public SurveySignaturePageViewModel GetSignaturePage(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var surveyName = connection.ExecuteScalar<string?>(
            @"SELECT name_survey
              FROM public.survey
              WHERE id_survey = @surveyId",
            new { surveyId }) ?? "Неизвестная анкета";

        const string sql = @"
            SELECT
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName,
                (ha.completion_date IS NOT NULL) AS IsCompleted,
                (COALESCE(ha.csp, '') <> '') AS IsSigned,
                ha.completion_date AS CompletionDate
            FROM public.organization o
            INNER JOIN public.organization_survey os
                ON os.id_organization = o.id_organization
            LEFT JOIN public.answer ha
                ON ha.id_organization_survey = os.id_organization_survey
            WHERE os.id_survey = @surveyId
            ORDER BY
                (ha.completion_date IS NOT NULL) DESC,
                ha.completion_date ASC NULLS LAST,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)";

        var items = connection.Query<SignatureRow>(sql, new { surveyId })
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
            SurveyName = surveyName,
            Items = items
        };
    }

    public AnswerStatisticsResponse GetStatistics()
    {
        return new AnswerStatisticsResponse
        {
            LineChart = BuildAverageScoreByYearChart(),
            BarChart = BuildAverageScoreByQuarterChart(),
            AvgScoreByOrganizationRadar = BuildAverageScoreByOrganizationChart()
        };
    }

    private SingleSeriesChartViewModel BuildAverageScoreByYearChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                EXTRACT(YEAR FROM ha.completion_date)::int AS Year,
                AVG(hai.rating::double precision) AS AverageRating
            FROM public.answer ha
            INNER JOIN public.answer_item hai
                ON hai.id_answer = ha.id_answer
            WHERE ha.completion_date IS NOT NULL
              AND hai.rating IS NOT NULL
            GROUP BY 1
            ORDER BY 1";

        var rows = connection.Query<AverageByYearRow>(sql).ToList();

        return new SingleSeriesChartViewModel
        {
            Labels = rows.Select(row => row.Year.ToString()).ToList(),
            Label = "Средняя оценка",
            Data = rows.Select(row => Math.Round(row.AverageRating, 2)).ToList()
        };
    }

    private SingleSeriesChartViewModel BuildAverageScoreByQuarterChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                EXTRACT(QUARTER FROM ha.completion_date)::int AS Quarter,
                AVG(hai.rating::double precision) AS AverageRating
            FROM public.answer ha
            INNER JOIN public.answer_item hai
                ON hai.id_answer = ha.id_answer
            WHERE ha.completion_date IS NOT NULL
              AND hai.rating IS NOT NULL
            GROUP BY 1
            ORDER BY 1";

        var averagesByQuarter = connection.Query<AverageByQuarterRow>(sql)
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

    private DatasetChartViewModel BuildAverageScoreByOrganizationChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName,
                AVG(hai.rating::double precision) AS AverageRating
            FROM public.answer ha
            INNER JOIN public.organization_survey os
                ON os.id_organization_survey = ha.id_organization_survey
            INNER JOIN public.organization o
                ON os.id_organization = o.id_organization
            INNER JOIN public.answer_item hai
                ON hai.id_answer = ha.id_answer
            WHERE ha.completion_date IS NOT NULL
              AND hai.rating IS NOT NULL
            GROUP BY 1
            ORDER BY 1";

        var rows = connection.Query<OrganizationAverageRow>(sql).ToList();
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

    private static bool MatchesDateBounds(DateTime? completionDate, DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            return true;
        }

        return completionDate.HasValue
            && completionDate.Value >= startDate.Value
            && completionDate.Value <= endDate.Value;
    }

    private static string NormalizeAnswerSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            AnswerListSortFields.Organization => AnswerListSortFields.Organization,
            AnswerListSortFields.Survey => AnswerListSortFields.Survey,
            AnswerListSortFields.Signed => AnswerListSortFields.Signed,
            _ => AnswerListSortFields.Date
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
            AnswerListSortFields.Date => "desc",
            AnswerListSortFields.Signed => "desc",
            _ => "asc"
        };
    }

    private static List<AnswerListItemViewModel> SortAnswerRows(
        IEnumerable<AnswerListItemViewModel> rows,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.Ordinal);
        IOrderedEnumerable<AnswerListItemViewModel> orderedRows = sortBy switch
        {
            AnswerListSortFields.Organization => descending
                ? rows.OrderByDescending(row => row.OrganizationName, AppListPaging.RuStringComparer)
                : rows.OrderBy(row => row.OrganizationName, AppListPaging.RuStringComparer),
            AnswerListSortFields.Survey => descending
                ? rows.OrderByDescending(row => row.SurveyName, AppListPaging.RuStringComparer)
                : rows.OrderBy(row => row.SurveyName, AppListPaging.RuStringComparer),
            AnswerListSortFields.Signed => descending
                ? rows.OrderByDescending(row => row.IsSigned)
                : rows.OrderBy(row => row.IsSigned),
            _ => descending
                ? rows.OrderByDescending(row => row.CompletionDate ?? DateTime.MinValue)
                : rows.OrderBy(row => row.CompletionDate ?? DateTime.MaxValue)
        };

        return orderedRows
            .ThenByDescending(row => row.IdAnswer)
            .ToList();
    }

    private enum AnswerDateFilterType
    {
        None,
        Year,
        Month,
        Range
    }

    private sealed class AnswerListRow
    {
        public int IdAnswer { get; set; }
        public int IdOrganization { get; set; }
        public int IdSurvey { get; set; }
        public string? OrganizationName { get; set; }
        public string? SurveyName { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? Signature { get; set; }
    }

    private sealed class SignatureRow
    {
        public string? OrganizationName { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? CompletionDate { get; set; }
    }

    private sealed class AverageByYearRow
    {
        public int Year { get; set; }
        public double AverageRating { get; set; }
    }

    private sealed class AverageByQuarterRow
    {
        public int Quarter { get; set; }
        public double AverageRating { get; set; }
    }

    private sealed class OrganizationAverageRow
    {
        public string? OrganizationName { get; set; }
        public double AverageRating { get; set; }
    }
}
