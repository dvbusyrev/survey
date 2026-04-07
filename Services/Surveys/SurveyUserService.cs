using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;

namespace main_project.Services.Surveys;

public sealed class SurveyUserService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyUserService(IDbConnectionFactory connectionFactory)
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

    public UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOmsuId = connection.ExecuteScalar<int?>(
            "SELECT id_omsu FROM public.users WHERE id_user = @userId",
            new { userId });

        if (!userOmsuId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var hasSearch = !string.IsNullOrWhiteSpace(normalizedSearchTerm);
        var parameters = new DynamicParameters();
        parameters.Add("userOmsuId", userOmsuId.Value);
        parameters.Add("hasSearch", hasSearch);
        parameters.Add("searchPattern", $"%{normalizedSearchTerm}%");
        parameters.Add("offset", Math.Max(currentPage - 1, 0) * pageSize);
        parameters.Add("pageSize", pageSize);

        const string baseSql = @"
            FROM (
                SELECT
                    s.id_survey,
                    s.name_survey,
                    s.description,
                    s.date_open::timestamp AS date_open,
                    s.date_close::timestamp AS date_close
                FROM public.surveys s
                INNER JOIN public.omsu_surveys os
                    ON os.id_survey = s.id_survey
                WHERE os.id_omsu = @userOmsuId
                  AND NOT EXISTS (
                      SELECT 1
                      FROM public.history_answer ha
                      WHERE ha.id_omsu = @userOmsuId
                        AND ha.id_survey = s.id_survey
                  )

                UNION

                SELECT
                    hs.id_survey,
                    hs.name_survey,
                    hs.description,
                    hs.date_begin::timestamp AS date_open,
                    COALESCE(ae.new_end_date::timestamp, hs.date_end::timestamp) AS date_close
                FROM public.history_surveys hs
                INNER JOIN public.access_extensions ae
                    ON hs.id_survey = ae.id_survey
                WHERE ae.id_omsu = @userOmsuId
                  AND ae.new_end_date > NOW()
            ) AS accessible
            WHERE (@hasSearch = FALSE OR accessible.name_survey ILIKE @searchPattern)";

        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {baseSql}",
            parameters);

        var surveys = connection.Query<Survey>(
            $@"SELECT
                    id_survey,
                    name_survey,
                    description,
                    date_open,
                    date_close
               {baseSql}
               ORDER BY id_survey DESC
               OFFSET @offset
               LIMIT @pageSize",
            parameters).ToList();

        foreach (var survey in surveys)
        {
            survey.id_omsu = userOmsuId.Value;
        }

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / pageSize);

        return new UserSurveyListPageViewModel
        {
            AccessibleSurveys = surveys,
            UserOmsuId = userOmsuId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = totalCount,
            SearchTerm = normalizedSearchTerm
        };
    }

    public IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = connection.Query<SurveyQuestionRow>(
            @"SELECT question_order AS QuestionOrder, question_text AS QuestionText
              FROM public.survey_questions
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId });

        if (rows.Any())
        {
            return rows
                .Select(q => new SurveyQuestionItem
                {
                    Id = q.QuestionOrder,
                    Text = q.QuestionText
                })
                .ToList();
        }

        return connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.history_survey_questions
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId })
            .Select(q => new SurveyQuestionItem
            {
                Id = q.Id,
                Text = q.Text
            })
            .ToList();
    }
}
