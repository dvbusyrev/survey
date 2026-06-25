using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Read;
using MainProject.Application.Support;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;

namespace MainProject.Infrastructure.Persistence;

public sealed class AnswerReadRepository : IAnswerReadRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AnswerReadRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Survey?> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                s.id_survey,
                s.name_survey,
                s.description,
                ss.date_begin,
                ss.date_end
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE s.id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT question_order AS Id, question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetSurveyAnswersAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_answer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_survey,
                assignment.id_organization AS OrganizationId,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name,
                answer.completion_date,
                answer.csp
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
            ORDER BY answer.completion_date DESC;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    public async Task<AnswerListReadData> GetListAsync(
        AnswerListReadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var parameters = new DynamicParameters();
        parameters.Add("SelectedOrganizationIds", request.OrganizationIds.ToArray());
        parameters.Add("HasOrganizationFilter", request.OrganizationIds.Count > 0);
        parameters.Add("SelectedSurveyIds", request.SurveyIds.ToArray());
        parameters.Add("HasSurveyFilter", request.SurveyIds.Count > 0);
        parameters.Add("HasDateFilter", request.DateStart.HasValue && request.DateEnd.HasValue);
        parameters.Add("DateStart", request.DateStart);
        parameters.Add("DateEnd", request.DateEnd);

        var organizationOptions = BuildSelectionOptions(await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            """
            SELECT DISTINCT
                assignment.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name, 'Нет данных') AS Name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization;
            """,
            cancellationToken: cancellationToken)));
        var surveyOptions = BuildSelectionOptions(await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            """
            SELECT DISTINCT
                assignment.id_survey AS Id,
                COALESCE(survey.name_survey, 'Нет данных') AS Name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey;
            """,
            cancellationToken: cancellationToken)));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{AnswerRowsCte} SELECT COUNT(*) FROM answer_rows WHERE {AnswerFilterPredicate};",
            parameters,
            cancellationToken: cancellationToken));

        var pageWindow = AppListPaging.CreateWindow(totalCount, request.CurrentPage, request.PageSize);
        parameters.Add("PageSize", pageWindow.PageSize);
        parameters.Add("Offset", pageWindow.Offset);
        var rows = (await connection.QueryAsync<AnswerListReadRow>(new CommandDefinition(
            $"""
            {AnswerRowsCte}
            SELECT
                id_answer AS IdAnswer,
                id_organization AS IdOrganization,
                id_survey AS IdSurvey,
                organization_name AS OrganizationName,
                survey_name AS SurveyName,
                completion_date AS CompletionDate,
                is_signed AS IsSigned
            FROM answer_rows
            WHERE {AnswerFilterPredicate}
            ORDER BY {BuildOrderBy(request.SortBy, request.SortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToList();

        return new AnswerListReadData(
            totalCount,
            pageWindow.CurrentPage,
            pageWindow.TotalPages,
            pageWindow.PageSize,
            rows,
            organizationOptions,
            surveyOptions);
    }

    public async Task<SurveySignatureReadData> GetSignatureStatusAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveyName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name_survey FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken)) ?? "Неизвестная анкета";
        var rows = (await connection.QueryAsync<SurveySignatureReadRow>(new CommandDefinition(
            """
            SELECT
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS OrganizationName,
                (answer.completion_date IS NOT NULL) AS IsCompleted,
                (COALESCE(answer.csp, '') <> '') AS IsSigned,
                answer.completion_date AS CompletionDate
            FROM public.organization organization
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization = organization.id_organization
            LEFT JOIN public.answer answer
                ON answer.id_organization_survey = assignment.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
            ORDER BY
                (answer.completion_date IS NOT NULL) DESC,
                answer.completion_date ASC NULLS LAST,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();

        return new SurveySignatureReadData(surveyName, rows);
    }

    public async Task<AnswerStatisticsReadData> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var byYear = (await connection.QueryAsync<AverageByYearReadRow>(new CommandDefinition(
            """
            SELECT EXTRACT(YEAR FROM answer.completion_date)::int AS Year,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();
        var byQuarter = (await connection.QueryAsync<AverageByQuarterReadRow>(new CommandDefinition(
            """
            SELECT EXTRACT(QUARTER FROM answer.completion_date)::int AS Quarter,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();
        var byOrganization = (await connection.QueryAsync<OrganizationAverageReadRow>(new CommandDefinition(
            """
            SELECT COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS OrganizationName,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();

        return new AnswerStatisticsReadData(byYear, byQuarter, byOrganization);
    }

    private static async Task AttachAnswerItemsAsync(
        System.Data.IDbConnection connection,
        IEnumerable<AnswerRecord> answers,
        CancellationToken cancellationToken)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(answer => answer.IdAnswer).Distinct().ToArray();
        var rows = await connection.QueryAsync<AnswerItemLookupRow>(new CommandDefinition(
            """
            SELECT id_answer AS AnswerId, question_order AS QuestionOrder, question_text AS QuestionText,
                   rating AS Rating, comment AS Comment
            FROM public.answer_item
            WHERE id_answer = ANY(@AnswerIds)
            ORDER BY id_answer, question_order;
            """,
            new { AnswerIds = answerIds },
            cancellationToken: cancellationToken));
        var lookup = rows.GroupBy(row => row.AnswerId).ToDictionary(
            group => group.Key,
            group => (List<AnswerPayloadItem>)group.Select(row => new AnswerPayloadItem
            {
                QuestionId = row.QuestionOrder.ToString(),
                QuestionText = row.QuestionText,
                Rating = row.Rating,
                Comment = row.Comment
            }).ToList());

        foreach (var answer in answerList)
        {
            answer.Answers = lookup.GetValueOrDefault(answer.IdAnswer, []);
        }
    }

    private static IReadOnlyList<SelectionOption> BuildSelectionOptions(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
    }

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal) ? "DESC" : "ASC";
        return sortBy switch
        {
            AnswerReadSortFields.Organization => $"organization_name {direction}, id_answer DESC",
            AnswerReadSortFields.Survey => $"survey_name {direction}, id_answer DESC",
            AnswerReadSortFields.Signed => $"is_signed {direction}, id_answer DESC",
            _ => $"completion_date {direction} NULLS LAST, id_answer DESC"
        };
    }

    private const string AnswerFilterPredicate = """
        (@HasOrganizationFilter = false OR id_organization = ANY(@SelectedOrganizationIds))
        AND (@HasSurveyFilter = false OR id_survey = ANY(@SelectedSurveyIds))
        AND (@HasDateFilter = false OR (completion_date IS NOT NULL AND completion_date >= @DateStart AND completion_date <= @DateEnd))
        """;

    private const string AnswerRowsCte = """
        WITH answer_rows AS (
            SELECT
                answer.id_answer,
                assignment.id_organization,
                assignment.id_survey,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name, 'Нет данных') AS organization_name,
                COALESCE(survey.name_survey, 'Нет данных') AS survey_name,
                answer.completion_date,
                (COALESCE(answer.csp, '') <> '') AS is_signed
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization ON organization.id_organization = assignment.id_organization
            LEFT JOIN public.survey survey ON survey.id_survey = assignment.id_survey
        )
        """;

    private sealed class AnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }
}
