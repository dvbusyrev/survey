using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
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

    public AnswerListPageViewModel GetAnswersPage()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                ha.id_answer AS IdAnswer,
                ha.id_organization AS IdOrganization,
                ha.id_survey AS IdSurvey,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, 'Нет данных') AS OrganizationName,
                COALESCE(s.name_survey, 'Нет данных') AS SurveyName,
                ha.completion_date AS CompletionDate,
                COALESCE(ha.csp, '') AS Signature
            FROM public.answer ha
            LEFT JOIN public.organization o
                ON o.id_organization = ha.id_organization
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
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS OrganizationName,
                (ha.completion_date IS NOT NULL) AS IsCompleted,
                (COALESCE(ha.csp, '') <> '') AS IsSigned
            FROM public.organization o
            INNER JOIN public.organization_survey os
                ON os.id_organization = o.id_organization
            LEFT JOIN public.answer ha
                ON o.id_organization = ha.id_organization
               AND ha.id_survey = @surveyId
            WHERE os.id_survey = @surveyId
            ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)";

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
            INNER JOIN public.organization o
                ON ha.id_organization = o.id_organization
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
