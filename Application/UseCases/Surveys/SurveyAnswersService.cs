using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Application.UseCases.Answers;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyAnswersService : ISurveyAnswersService
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
            @"SELECT
                  s.id_survey,
                  s.name_survey,
                  s.description,
                  ss.date_begin,
                  ss.date_end
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                  ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @surveyId",
            new { surveyId });

        if (survey == null)
        {
            return null;
        }

        var answers = connection.Query<AnswerRecord>(
            @"SELECT
                  ha.id_answer,
                  ha.id_organization_survey AS IdOrganizationSurvey,
                  os.id_survey,
                  os.id_organization AS OrganizationId,
                  COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS organization_name,
                  ha.completion_date,
                  ha.csp
              FROM public.answer ha
              INNER JOIN public.organization_survey os
                  ON os.id_organization_survey = ha.id_organization_survey
              INNER JOIN public.organization o
                  ON o.id_organization = os.id_organization
              WHERE os.id_survey = @surveyId
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
            @"SELECT
                  s.id_survey,
                  s.name_survey,
                  s.description,
                  ss.date_begin,
                  ss.date_end
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                  ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @surveyId",
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
                  ha.id_organization_survey AS IdOrganizationSurvey,
                  os.id_organization AS OrganizationId,
                  os.id_survey,
                  COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS organization_name,
                  ha.csp,
                  ha.completion_date
              FROM public.answer ha
              INNER JOIN public.organization_survey os
                  ON os.id_organization_survey = ha.id_organization_survey
              INNER JOIN public.organization o
                  ON os.id_organization = o.id_organization
              WHERE os.id_survey = @surveyId
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
