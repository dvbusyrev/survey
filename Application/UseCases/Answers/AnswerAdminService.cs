using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerAdminService : IAnswerAdminService
{
    private static readonly string[] RussianMonthNames =
    {
        "Январь",
        "Февраль",
        "Март",
        "Апрель",
        "Май",
        "Июнь",
        "Июль",
        "Август",
        "Сентябрь",
        "Октябрь",
        "Ноябрь",
        "Декабрь"
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public AnswerAdminService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public AnswerListPageViewModel GetAnswersPage()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                ha.id_answer AS IdAnswer,
                ha.organization_id AS IdOrganization,
                ha.id_survey AS IdSurvey,
                COALESCE(o.organization_name, 'Нет данных') AS OrganizationName,
                COALESCE(s.name_survey, 'Нет данных') AS SurveyName,
                ha.completion_date AS CompletionDate,
                COALESCE(ha.csp, '') AS Signature
            FROM public.answer ha
            LEFT JOIN public.organization o
                ON o.organization_id = ha.organization_id
            LEFT JOIN public.survey s
                ON s.id_survey = ha.id_survey
            ORDER BY ha.completion_date DESC NULLS LAST, ha.id_answer DESC";

        var rows = connection.Query<AnswerListRow>(sql).ToList();

        return new AnswerListPageViewModel
        {
            Answers = rows.Select(row => new AnswerListItemViewModel
            {
                IdAnswer = row.IdAnswer,
                IdOrganization = row.IdOrganization,
                IdSurvey = row.IdSurvey,
                OrganizationName = row.OrganizationName ?? "Нет данных",
                SurveyName = row.SurveyName ?? "Нет данных",
                CompletionDate = row.CompletionDate,
                IsSigned = !string.IsNullOrWhiteSpace(row.Signature)
            }).ToList()
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
                o.organization_name AS OrganizationName,
                (ha.completion_date IS NOT NULL) AS IsCompleted,
                (COALESCE(ha.csp, '') <> '') AS IsSigned
            FROM public.organization o
            INNER JOIN public.organization_survey os
                ON os.organization_id = o.organization_id
            LEFT JOIN public.answer ha
                ON o.organization_id = ha.organization_id
               AND ha.id_survey = @surveyId
            WHERE os.id_survey = @surveyId
            ORDER BY o.organization_name";

        var items = connection.Query<SignatureRow>(sql, new { surveyId })
            .Select(row => new SurveySignatureStatusViewModel
            {
                OrganizationName = row.OrganizationName ?? string.Empty,
                IsCompleted = row.IsCompleted,
                IsSigned = row.IsSigned
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
            LineChart = BuildLineChart(),
            PieChart = BuildPieChart(),
            BarChart = BuildBarChart(),
            RadarChart = BuildRadarChart(),
            AvgScoreByOrganizationRadar = BuildAvgScoreByOrganizationRadar()
        };
    }

    private SingleSeriesChartViewModel BuildLineChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                EXTRACT(YEAR FROM completion_date)::int AS Year,
                EXTRACT(MONTH FROM completion_date)::int AS Month,
                COUNT(*)::int AS Count
            FROM public.answer
            WHERE completion_date IS NOT NULL
            GROUP BY 1, 2
            ORDER BY 1, 2";

        var rows = connection.Query<CompletionByMonthRow>(sql).ToList();

        return new SingleSeriesChartViewModel
        {
            Labels = rows.Select(row => FormatMonthYear(row.Year, row.Month)).ToList(),
            Label = "Количество ответов",
            Data = rows.Select(row => (double)row.Count).ToList()
        };
    }

    private SingleSeriesChartViewModel BuildPieChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                COALESCE(s.name_survey, 'Неизвестно') AS Label,
                COUNT(*)::int AS Count
            FROM public.answer ha
            LEFT JOIN public.survey s
                ON ha.id_survey = s.id_survey
            GROUP BY 1
            ORDER BY 1";

        var rows = connection.Query<LabelCountRow>(sql).ToList();

        return new SingleSeriesChartViewModel
        {
            Labels = rows.Select(row => row.Label ?? "Неизвестно").ToList(),
            Data = rows.Select(row => (double)row.Count).ToList()
        };
    }

    private SingleSeriesChartViewModel BuildBarChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                EXTRACT(YEAR FROM completion_date)::int AS Year,
                COUNT(*)::int AS Count
            FROM public.answer
            WHERE completion_date IS NOT NULL
            GROUP BY 1
            ORDER BY 1";

        var rows = connection.Query<YearCountRow>(sql).ToList();

        return new SingleSeriesChartViewModel
        {
            Labels = rows.Select(row => row.Year.ToString()).ToList(),
            Label = "Количество ответов",
            Data = rows.Select(row => (double)row.Count).ToList()
        };
    }

    private DatasetChartViewModel BuildRadarChart()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                COALESCE(s.name_survey, 'Неизвестно') AS SurveyType,
                hai.question_text AS QuestionLabel,
                AVG(hai.rating::double precision) AS AvgRating
            FROM public.answer ha
            LEFT JOIN public.survey s
                ON ha.id_survey = s.id_survey
            INNER JOIN public.answer_item hai
                ON hai.id_answer = ha.id_answer
            WHERE hai.rating IS NOT NULL
            GROUP BY 1, 2
            ORDER BY 1, 2";

        var rows = connection.Query<RadarRow>(sql).ToList();
        return BuildDatasetChart(rows);
    }

    private DatasetChartViewModel BuildAvgScoreByOrganizationRadar()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                o.organization_name AS OrganizationName,
                EXTRACT(YEAR FROM ha.completion_date)::int AS Year,
                AVG(hai.rating::double precision) AS AvgRating
            FROM public.answer ha
            INNER JOIN public.organization o
                ON ha.organization_id = o.organization_id
            INNER JOIN public.answer_item hai
                ON hai.id_answer = ha.id_answer
            WHERE ha.completion_date IS NOT NULL
              AND hai.rating IS NOT NULL
            GROUP BY 1, 2
            ORDER BY 2, 1";

        var rows = connection.Query<OrganizationAverageRow>(sql).ToList();
        if (rows.Count == 0)
        {
            return new DatasetChartViewModel();
        }

        var years = rows
            .Select(row => row.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .Take(3)
            .OrderBy(year => year)
            .ToList();

        var labels = rows
            .Select(row => row.OrganizationName ?? "Неизвестно")
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var colors = new[] { "rgba(54,162,235,0.2)", "rgba(255,99,132,0.2)", "rgba(255,206,86,0.2)" };
        var borderColors = new[] { "rgb(54,162,235)", "rgb(255,99,132)", "rgb(255,206,86)" };

        var datasets = years
            .Select((year, index) => new ChartDatasetViewModel
            {
                Label = year.ToString(),
                Data = labels
                    .Select(label => rows
                        .Where(row => row.Year == year && (row.OrganizationName ?? "Неизвестно") == label)
                        .Select(row => row.AvgRating)
                        .DefaultIfEmpty(0)
                        .First())
                    .ToList(),
                BackgroundColor = colors[index % colors.Length],
                BorderColor = borderColors[index % borderColors.Length]
            })
            .ToList();

        return new DatasetChartViewModel
        {
            Labels = labels,
            Datasets = datasets
        };
    }

    private static DatasetChartViewModel BuildDatasetChart(IEnumerable<RadarRow> rows)
    {
        var labels = new List<string>();
        var labelSet = new HashSet<string>(StringComparer.Ordinal);
        var valuesByGroup = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var group = string.IsNullOrWhiteSpace(row.SurveyType) ? "Неизвестно" : row.SurveyType;
            var label = string.IsNullOrWhiteSpace(row.QuestionLabel) ? "Без названия" : row.QuestionLabel;

            if (labelSet.Add(label))
            {
                labels.Add(label);
            }

            if (!valuesByGroup.TryGetValue(group, out var values))
            {
                values = new Dictionary<string, double>(StringComparer.Ordinal);
                valuesByGroup[group] = values;
            }

            values[label] = row.AvgRating;
        }

        return new DatasetChartViewModel
        {
            Labels = labels,
            Datasets = valuesByGroup.Select(group => new ChartDatasetViewModel
            {
                Label = group.Key,
                Data = labels
                    .Select(label => group.Value.TryGetValue(label, out var value) ? value : 0)
                    .ToList()
            }).ToList()
        };
    }

    private static string FormatMonthYear(int year, int month)
    {
        if (month < 1 || month > RussianMonthNames.Length)
        {
            return year.ToString();
        }

        return $"{RussianMonthNames[month - 1]} {year}";
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
    }

    private sealed class CompletionByMonthRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }

    private sealed class LabelCountRow
    {
        public string? Label { get; set; }
        public int Count { get; set; }
    }

    private sealed class YearCountRow
    {
        public int Year { get; set; }
        public int Count { get; set; }
    }

    private sealed class RadarRow
    {
        public string? SurveyType { get; set; }
        public string? QuestionLabel { get; set; }
        public double AvgRating { get; set; }
    }

    private sealed class OrganizationAverageRow
    {
        public string? OrganizationName { get; set; }
        public int Year { get; set; }
        public double AvgRating { get; set; }
    }
}
