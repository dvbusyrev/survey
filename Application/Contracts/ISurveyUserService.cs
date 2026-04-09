using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyUserService
{
    int? GetUserOrganizationId(int userId);
    bool IsSurveyAssignedToOrganization(int surveyId, int organizationId);
    UserSurveyListPageViewModel? GetActiveSurveysPage(int userId, int currentPage, string? searchTerm);
    IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId);
}
