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
    private readonly IAnswerReadRepository _answerReadRepository;

    public SurveyUserService(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository,
        IAnswerReadRepository answerReadRepository)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
        _answerReadRepository = answerReadRepository;
    }

    public async Task<int?> GetUserOrganizationIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _assignmentRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);
    }

    public async Task<bool> IsSurveyAssignedToOrganizationAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _assignmentRepository.IsActiveAssignmentAsync(connection, surveyId, organizationId, cancellationToken);
    }

    public async Task<UserSurveyListPageViewModel?> GetActiveSurveysPageAsync(
        int userId,
        int currentPage,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var userOrganizationId = await _assignmentRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var pageData = await _assignmentRepository.GetActiveUserSurveyPageAsync(
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

    public Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default)
        => _answerReadRepository.GetSurveyAsync(surveyId, cancellationToken);

    public Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
        => _answerReadRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken);
}
