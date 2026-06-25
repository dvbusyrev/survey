using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyUserService
{
    Task<int?> GetUserOrganizationIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> IsSurveyAssignedToOrganizationAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default);
    Task<UserSurveyListPageViewModel?> GetActiveSurveysPageAsync(int userId, int currentPage, string? searchTerm, CancellationToken cancellationToken = default);
    Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(int surveyId, CancellationToken cancellationToken = default);
}
