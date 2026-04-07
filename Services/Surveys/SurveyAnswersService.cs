using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;
using Newtonsoft.Json;

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
                  ha.answers,
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
            Details = DeserializeAnswers(answer.answers)
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
                  ha.answers
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

    private static IReadOnlyList<SurveyAnswerDetailViewModel> DeserializeAnswers(string? answersJson)
    {
        if (string.IsNullOrWhiteSpace(answersJson))
        {
            return Array.Empty<SurveyAnswerDetailViewModel>();
        }

        try
        {
            IReadOnlyList<SurveyAnswerDetailViewModel>? parsedAnswers =
                JsonConvert.DeserializeObject<List<SurveyAnswerDetailViewModel>>(answersJson);
            return parsedAnswers ?? Array.Empty<SurveyAnswerDetailViewModel>();
        }
        catch
        {
            return Array.Empty<SurveyAnswerDetailViewModel>();
        }
    }
}
