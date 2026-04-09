using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyAdminService
{
    List<Survey> GetSurveys();
    Task<SurveyCommandResult> CreateSurveyAsync(SurveyAddRequest? request);
    SurveyEditPageViewModel? GetSurveyEditPage(int id);
    SurveyCommandResult UpdateSurvey(int id, SurveyUpdateRequest? model);
    Survey? GetSurveyForCopy(int id);
    Task<SurveyCommandResult> CopySurveyAsync(int id, SurveyCopyRequest? request);
    List<Survey>? DeleteSurvey(int surveyId);
}
