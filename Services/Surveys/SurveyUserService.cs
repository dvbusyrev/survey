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

    public int? GetUserOrganizationId(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<int?>(
            "SELECT organization_id FROM public.app_user WHERE id_user = @userId",
            new { userId });
    }

    public UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOrganizationId = connection.ExecuteScalar<int?>(
            "SELECT organization_id FROM public.app_user WHERE id_user = @userId",
            new { userId });

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var hasSearch = !string.IsNullOrWhiteSpace(normalizedSearchTerm);
        var parameters = new DynamicParameters();
        parameters.Add("userOrganizationId", userOrganizationId.Value);
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
                    GREATEST(COALESCE(os.extended_until, s.date_close), s.date_close)::timestamp AS date_close
                FROM public.survey s
                INNER JOIN public.organization_survey os
                    ON os.id_survey = s.id_survey
                WHERE os.organization_id = @userOrganizationId
                  AND GREATEST(COALESCE(os.extended_until, s.date_close), s.date_close) > NOW()
                  AND NOT EXISTS (
                      SELECT 1
                      FROM public.answer a
                      WHERE a.organization_id = @userOrganizationId
                        AND a.id_survey = s.id_survey
                  )
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
            survey.organization_id = userOrganizationId.Value;
        }

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / pageSize);

        return new UserSurveyListPageViewModel
        {
            AccessibleSurveys = surveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = totalCount,
            SearchTerm = normalizedSearchTerm
        };
    }

    public IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<SurveyQuestionRow>(
            @"SELECT question_order AS QuestionOrder, question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId })
            .Select(q => new SurveyQuestionItem
            {
                Id = q.QuestionOrder,
                Text = q.QuestionText
            })
            .ToList();
    }
}
