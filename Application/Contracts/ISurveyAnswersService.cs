using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyAnswersService
{
    SurveyAnswerPageViewModel? GetSurveyAnswerPage(int surveyId, string role);
    object GetSurveyAnswersResponse(int surveyId);
}
