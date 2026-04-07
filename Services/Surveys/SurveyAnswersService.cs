using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;
namespace main_project.Services.Surveys;

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
              FROM (
                  SELECT
                      id_survey,
                      name_survey,
                      description,
                      date_open,
                      date_close
                  FROM public.surveys
                  WHERE id_survey = @surveyId

                  UNION ALL

                  SELECT
                      id_survey,
                      name_survey,
                      description,
                      date_begin AS date_open,
                      date_end AS date_close
                  FROM public.history_surveys
                  WHERE id_survey = @surveyId
              ) AS survey_data
              LIMIT 1",
            new { surveyId });

        if (survey == null)
        {
            return null;
        }

        var answers = connection.Query<HistoryAnswer>(
            @"SELECT
                  ha.id_answer,
                  ha.id_survey,
                  ha.id_omsu,
                  o.name_omsu,
                  ha.completion_date,
                  ha.create_date_survey,
                  COALESCE(
                      (
                          SELECT jsonb_agg(
                              jsonb_build_object(
                                  'question_id', hai.question_order,
                                  'question_text', hai.question_text,
                                  'rating', hai.rating,
                                  'comment', hai.comment
                              )
                              ORDER BY hai.question_order
                          )::text
                          FROM public.history_answer_items hai
                          WHERE hai.id_answer = ha.id_answer
                      ),
                      '[]'
                  ) AS answers,
                  ha.csp
              FROM public.history_answer ha
              INNER JOIN public.omsu o
                  ON o.id_omsu = ha.id_omsu
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        var mappedAnswers = answers.Select(answer => new SurveyAnswerEntryViewModel
        {
            IdAnswer = answer.id_answer,
            IdOmsu = answer.id_omsu,
            IdSurvey = answer.id_survey,
            NameOmsu = answer.name_omsu ?? string.Empty,
            Csp = answer.csp,
            CompletionDate = answer.completion_date,
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
              FROM (
                  SELECT
                      id_survey,
                      name_survey,
                      description,
                      date_open,
                      date_close
                  FROM public.surveys
                  WHERE id_survey = @surveyId

                  UNION ALL

                  SELECT
                      id_survey,
                      name_survey,
                      description,
                      date_begin AS date_open,
                      date_end AS date_close
                  FROM public.history_surveys
                  WHERE id_survey = @surveyId
              ) AS survey_data
              LIMIT 1",
            new { surveyId });

        if (survey == null)
        {
            return new
            {
                success = false,
                error = "Анкета не найдена"
            };
        }

        var answers = connection.Query<HistoryAnswer>(
            @"SELECT
                  ha.id_answer,
                  ha.id_omsu,
                  ha.id_survey,
                  o.name_omsu,
                  ha.csp,
                  ha.completion_date,
                  COALESCE(
                      (
                          SELECT jsonb_agg(
                              jsonb_build_object(
                                  'question_id', hai.question_order,
                                  'question_text', hai.question_text,
                                  'rating', hai.rating,
                                  'comment', hai.comment
                              )
                              ORDER BY hai.question_order
                          )::text
                          FROM public.history_answer_items hai
                          WHERE hai.id_answer = ha.id_answer
                      ),
                      '[]'
                  ) AS answers
              FROM public.history_answer ha
              INNER JOIN public.omsu o
                  ON ha.id_omsu = o.id_omsu
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        return new
        {
            success = true,
            survey,
            answers
        };
    }
}
