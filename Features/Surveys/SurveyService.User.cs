using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    public async Task<int?> GetUserOrganizationIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);
    }

    public async Task<bool> IsSurveyAssignedToOrganizationAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.IsActiveAssignmentAsync(connection, surveyId, organizationId, cancellationToken);
    }

    public async Task<UserSurveyListPageViewModel?> GetActiveSurveysPageAsync(
        int userId,
        int currentPage,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var userOrganizationId = await _surveyRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var pageData = await _surveyRepository.GetActiveUserSurveyPageAsync(
            connection,
            userOrganizationId.Value,
            normalizedSearchTerm,
            pageSize,
            Math.Max(currentPage - 1, 0) * pageSize,
            cancellationToken);
        var surveys = pageData.Surveys.ToList();

        foreach (var survey in surveys)
        {
            survey.OrganizationId = userOrganizationId.Value;
        }

        var pageWindow = AppListPaging.CreateWindow(pageData.TotalCount, currentPage, pageSize);

        return new UserSurveyListPageViewModel
        {
            AccessibleSurveys = surveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            SearchTerm = normalizedSearchTerm
        };
    }

    public Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyAsync(surveyId, cancellationToken);

    public Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken);
}
