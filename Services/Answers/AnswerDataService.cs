using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;

namespace main_project.Services.Answers;

public sealed class AnswerDataService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AnswerDataService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public int? GetUserOmsuId(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<int?>(
            "SELECT id_omsu FROM public.users WHERE id_user = @userId",
            new { userId });
    }

    public Survey? GetSurveyInfo(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<Survey>(
            @"SELECT
                  id_survey,
                  name_survey,
                  description
              FROM (
                  SELECT
                      id_survey,
                      name_survey,
                      description
                  FROM public.surveys
                  WHERE id_survey = @surveyId

                  UNION ALL

                  SELECT
                      id_survey,
                      name_survey,
                      description
                  FROM public.history_surveys
                  WHERE id_survey = @surveyId
              ) AS survey_data
              LIMIT 1",
            new { surveyId });
    }

    public string? GetSurveyQuestionsJson(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<string?>(
            @"SELECT questions_json
              FROM (
                  SELECT
                      questions::text AS questions_json,
                      0 AS priority
                  FROM public.surveys
                  WHERE id_survey = @surveyId

                  UNION ALL

                  SELECT
                      file_questions::text AS questions_json,
                      1 AS priority
                  FROM public.history_surveys
                  WHERE id_survey = @surveyId
              ) AS survey_questions
              ORDER BY priority
              LIMIT 1",
            new { surveyId });
    }

    public HistoryAnswer? GetHistoryAnswer(int surveyId, int omsuId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<HistoryAnswer>(
            @"SELECT
                  id_answer,
                  id_omsu,
                  id_survey,
                  completion_date,
                  create_date_survey,
                  answers,
                  csp
              FROM public.history_answer
              WHERE id_survey = @surveyId
                AND id_omsu = @omsuId",
            new { surveyId, omsuId });
    }

    public IReadOnlyList<HistoryAnswer> GetHistoryAnswers(int surveyId, int? omsuId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (omsuId.HasValue)
        {
            return connection.Query<HistoryAnswer>(
                @"SELECT
                      ha.id_answer,
                      ha.id_omsu,
                      ha.id_survey,
                      ha.csp,
                      ha.completion_date,
                      ha.create_date_survey,
                      ha.answers,
                      o.name_omsu
                  FROM public.history_answer ha
                  LEFT JOIN public.omsu o
                      ON o.id_omsu = ha.id_omsu
                  WHERE ha.id_survey = @surveyId
                    AND ha.id_omsu = @omsuId
                  ORDER BY ha.completion_date DESC",
                new { surveyId, omsuId }).ToList();
        }

        return connection.Query<HistoryAnswer>(
            @"SELECT
                  ha.id_answer,
                  ha.id_omsu,
                  ha.id_survey,
                  ha.csp,
                  ha.completion_date,
                  ha.create_date_survey,
                  ha.answers,
                  o.name_omsu
              FROM public.history_answer ha
              LEFT JOIN public.omsu o
                  ON o.id_omsu = ha.id_omsu
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();
    }

    public int InsertHistoryAnswer(HistoryAnswer historyAnswerData)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<int>(
            @"INSERT INTO public.history_answer (
                  id_omsu,
                  id_survey,
                  completion_date,
                  answers,
                  create_date_survey
              )
              VALUES (
                  @idOmsu,
                  @idSurvey,
                  @completionDate,
                  CAST(@answers AS jsonb),
                  (SELECT date_create FROM public.surveys WHERE id_survey = @idSurvey)
              )
              RETURNING id_answer",
            new
            {
                idOmsu = historyAnswerData.id_omsu,
                idSurvey = historyAnswerData.id_survey,
                completionDate = DateTime.Now,
                answers = string.IsNullOrWhiteSpace(historyAnswerData.answers) ? "[]" : historyAnswerData.answers
            });
    }

    public bool UpdateHistoryAnswer(HistoryAnswer historyAnswerData)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rowsAffected = connection.Execute(
            @"UPDATE public.history_answer
              SET completion_date = @completionDate,
                  answers = CAST(@answers AS jsonb)
              WHERE id_omsu = @idOmsu
                AND id_survey = @idSurvey",
            new
            {
                idOmsu = historyAnswerData.id_omsu,
                idSurvey = historyAnswerData.id_survey,
                completionDate = DateTime.Now,
                answers = historyAnswerData.answers ?? "[]"
            });

        return rowsAffected > 0;
    }

    public bool UpdateSignature(int surveyId, int omsuId, string signature)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rowsAffected = connection.Execute(
            @"UPDATE public.history_answer
              SET csp = @signature
              WHERE id_omsu = @omsuId
                AND id_survey = @surveyId",
            new { signature, omsuId, surveyId });

        return rowsAffected > 0;
    }

    public void DeleteAccessExtension(int omsuId, int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        connection.Execute(
            @"DELETE FROM public.access_extensions
              WHERE id_omsu = @omsuId
                AND id_survey = @surveyId",
            new { omsuId, surveyId });
    }
}
