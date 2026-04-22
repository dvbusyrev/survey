using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyUserService : ISurveyUserService
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
            "SELECT id_organization FROM public.app_user WHERE id_user = @userId",
            new { userId });
    }

    public bool IsSurveyAssignedToOrganization(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<bool>(
            @"SELECT EXISTS (
                  SELECT 1
                  FROM public.organization_survey os
                  WHERE os.id_survey = @surveyId
                    AND os.id_organization = @organizationId
                    AND os.date_begin <= CURRENT_DATE
                    AND os.date_end >= CURRENT_DATE
              )",
            new { surveyId, organizationId });
    }

    public UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOrganizationId = connection.ExecuteScalar<int?>(
            "SELECT id_organization FROM public.app_user WHERE id_user = @userId",
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
                    os.date_begin,
                    os.date_end
                FROM public.survey s
                INNER JOIN public.organization_survey os
                    ON os.id_survey = s.id_survey
                WHERE os.id_organization = @userOrganizationId
                  AND os.date_begin <= CURRENT_DATE
                  AND os.date_end >= CURRENT_DATE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM public.answer a
                      WHERE a.id_organization = @userOrganizationId
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
                    date_begin,
                    date_end
               {baseSql}
               ORDER BY id_survey DESC
               OFFSET @offset
               LIMIT @pageSize",
            parameters).ToList();

        foreach (var survey in surveys)
        {
            survey.OrganizationId = userOrganizationId.Value;
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

    public Survey? GetSurveyInfo(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<Survey>(
            @"SELECT
                  id_survey,
                  name_survey,
                  description
              FROM public.survey
              WHERE id_survey = @surveyId",
            new { surveyId });
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
