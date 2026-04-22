using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyUserService
{
    int? GetUserOrganizationId(int userId);
    bool IsSurveyAssignedToOrganization(int surveyId, int organizationId);
    UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm);
    Survey? GetSurveyInfo(int surveyId);
    IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId);
}
