using System.Data;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;

namespace MainProject.Infrastructure.Persistence;

public sealed class SurveyReportRepository : ISurveyReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT DISTINCT EXTRACT(YEAR FROM report_year)::integer AS report_year
            FROM public.survey_schedule schedule
            CROSS JOIN LATERAL generate_series(
                date_trunc('year', schedule.date_begin),
                date_trunc('year', schedule.date_end),
                interval '1 year'
            ) AS report_year
            WHERE schedule.date_begin IS NOT NULL
              AND schedule.date_end IS NOT NULL
            ORDER BY report_year DESC;
            """,
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<string?> GetSurveyNameAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name_survey FROM public.survey WHERE id_survey = @SurveyId;",
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
        int? organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_answer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                assignment.id_survey,
                answer.csp,
                answer.completion_date,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
              AND (@OrganizationId IS NULL OR assignment.id_organization = @OrganizationId)
              AND EXISTS (
                  SELECT 1 FROM public.answer_item answer_item
                  WHERE answer_item.id_answer = answer.id_answer
              )
            ORDER BY answer.completion_date DESC;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId },
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    public async Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveys = (await connection.QueryAsync<Survey>(new CommandDefinition(
            """
            SELECT
                survey.id_survey,
                survey.name_survey,
                schedule.date_end,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name),
                            ', '
                        )
                        FROM public.organization_survey assignment
                        INNER JOIN public.organization organization
                            ON organization.id_organization = assignment.id_organization
                        WHERE assignment.id_survey = survey.id_survey
                    ),
                    'Не указано'
                ) AS organization_name
            FROM public.survey survey
            LEFT JOIN public.survey_schedule schedule
                ON schedule.id_survey = survey.id_survey;
            """,
            cancellationToken: cancellationToken))).ToList();

        await AttachSurveyQuestionsAsync(connection, surveys, cancellationToken);
        return surveys;
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name,
                answer.csp,
                answer.id_answer,
                assignment.id_survey,
                survey.name_survey,
                answer.completion_date
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            LEFT JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey
            WHERE EXISTS (
                SELECT 1 FROM public.answer_item answer_item
                WHERE answer_item.id_answer = answer.id_answer
            )
            ORDER BY answer.completion_date DESC;
            """,
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    private static async Task AttachSurveyQuestionsAsync(
        IDbConnection connection,
        IEnumerable<Survey> surveys,
        CancellationToken cancellationToken)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(survey => survey.IdSurvey).Distinct().ToArray();
        var rows = await connection.QueryAsync<SurveyQuestionLookupRow>(new CommandDefinition(
            """
            SELECT id_survey AS SurveyId, question_order AS QuestionOrder, question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = ANY(@SurveyIds)
            ORDER BY id_survey, question_order;
            """,
            new { SurveyIds = surveyIds },
            cancellationToken: cancellationToken));
        var lookup = rows.GroupBy(row => row.SurveyId).ToDictionary(
            group => group.Key,
            group => group.Select(row => new SurveyQuestionItem
            {
                Id = row.QuestionOrder,
                Text = row.QuestionText
            }).ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = lookup.GetValueOrDefault(survey.IdSurvey, []);
        }
    }

    private static async Task AttachAnswerItemsAsync(
        IDbConnection connection,
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
            SELECT
                id_answer AS AnswerId,
                question_order AS QuestionOrder,
                question_text AS QuestionText,
                rating AS Rating,
                comment AS Comment
            FROM public.answer_item
            WHERE id_answer = ANY(@AnswerIds)
            ORDER BY id_answer, question_order;
            """,
            new { AnswerIds = answerIds },
            cancellationToken: cancellationToken));
        var lookup = rows.GroupBy(row => row.AnswerId).ToDictionary(
            group => group.Key,
            group => group.Select(row => new AnswerPayloadItem
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

    private sealed class SurveyQuestionLookupRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }

    private sealed class AnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }
}
