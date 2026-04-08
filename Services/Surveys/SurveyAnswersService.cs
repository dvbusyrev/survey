using Dapper;
using MainProject.Infrastructure.Database;
using MainProject.Models;
using MainProject.Services.Answers;

namespace MainProject.Services.Surveys;

public sealed class SurveyAnswersService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyAnswersService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public SurveyAnswerPageViewModel? GetSurveyAnswerPage(int surveyId, string role)
    {
        using var connection = _connectionFactory.CreateConnection();

        var survey = connection.QueryFirstOrDefault<Survey>(
            @"SELECT *
              FROM public.survey
              WHERE id_survey = @surveyId",
            new { surveyId });

        if (survey == null)
        {
            return null;
        }

        var answers = connection.Query<AnswerRecord>(
            @"SELECT
                  ha.id_answer,
                  ha.id_survey,
                  ha.organization_id,
                  o.organization_name,
                  ha.completion_date,
                  ha.create_date_survey,
                  ha.csp
              FROM public.answer ha
              INNER JOIN public.organization o
                  ON o.organization_id = ha.organization_id
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        AttachAnswerItems(connection, answers);

        var mappedAnswers = answers.Select(answer => new SurveyAnswerEntryViewModel
        {
            IdAnswer = answer.IdAnswer,
            IdOrganization = answer.OrganizationId,
            IdSurvey = answer.IdSurvey,
            NameOrganization = answer.OrganizationName ?? string.Empty,
            Csp = answer.Csp,
            CompletionDate = answer.CompletionDate,
            Details = answer.Answers.Select(item => new SurveyAnswerDetailViewModel
            {
                QuestionText = item.DisplayQuestion,
                Rating = item.Rating?.ToString(),
                Comment = item.Comment
            }).ToList()
        }).ToList();

        return new SurveyAnswerPageViewModel
        {
            Survey = survey,
            Answers = mappedAnswers,
            Role = role
        };
    }

    public object GetSurveyAnswersResponse(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var survey = connection.QueryFirstOrDefault<Survey>(
            @"SELECT *
              FROM public.survey
              WHERE id_survey = @surveyId",
            new { surveyId });

        if (survey == null)
        {
            return new
            {
                success = false,
                error = "Анкета не найдена"
            };
        }

        var answers = connection.Query<AnswerRecord>(
            @"SELECT
                  ha.id_answer,
                  ha.organization_id,
                  ha.id_survey,
                  o.organization_name,
                  ha.csp,
                  ha.completion_date,
                  ha.create_date_survey
              FROM public.answer ha
              INNER JOIN public.organization o
                  ON ha.organization_id = o.organization_id
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        AttachAnswerItems(connection, answers);

        return new
        {
            success = true,
            survey,
            answers
        };
    }

    private static void AttachAnswerItems(
        global::System.Data.IDbConnection connection,
        IEnumerable<AnswerRecord> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(answer => answer.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<AnswerItemLookupRow>(
            @"SELECT
                  id_answer AS AnswerId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText,
                  rating AS Rating,
                  comment AS Comment
              FROM public.answer_item
              WHERE id_answer = ANY(@answerIds)
              ORDER BY id_answer, question_order",
            new { answerIds });

        var lookup = rows
            .GroupBy(row => row.AnswerId)
            .ToDictionary(
                group => group.Key,
                group => (List<AnswerPayloadItem>)group
                    .Select(row => new AnswerPayloadItem
                    {
                        QuestionId = row.QuestionOrder.ToString(),
                        QuestionText = row.QuestionText,
                        Rating = row.Rating,
                        Comment = row.Comment
                    })
                    .ToList());

        foreach (var answer in answerList)
        {
            answer.Answers = lookup.GetValueOrDefault(answer.IdAnswer, new List<AnswerPayloadItem>());
        }
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
