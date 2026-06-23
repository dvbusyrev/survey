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
    private readonly ISurveyAssignmentRepository _assignmentRepository;

    public SurveyUserService(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
    }

    public int? GetUserOrganizationId(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return _assignmentRepository.GetUserOrganizationId(connection, userId);
    }

    public bool IsSurveyAssignedToOrganization(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return _assignmentRepository.IsActiveAssignment(connection, surveyId, organizationId);
    }

    public UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOrganizationId = _assignmentRepository.GetUserOrganizationId(connection, userId);

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var pageData = _assignmentRepository.GetActiveUserSurveyPage(
            connection,
            userOrganizationId.Value,
            normalizedSearchTerm,
            pageSize,
            Math.Max(currentPage - 1, 0) * pageSize);
        var surveys = pageData.Surveys.ToList();

        foreach (var survey in surveys)
        {
            survey.OrganizationId = userOrganizationId.Value;
        }

        var totalPages = pageData.TotalCount == 0
            ? 1
            : (int)Math.Ceiling((double)pageData.TotalCount / pageSize);

        return new UserSurveyListPageViewModel
        {
            AccessibleSurveys = surveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = pageData.TotalCount,
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
