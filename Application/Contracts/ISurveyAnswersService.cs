using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyAnswersService
{
    Task<SurveyAnswerPageViewModel?> GetSurveyAnswerPageAsync(
        int surveyId,
        string role,
        CancellationToken cancellationToken = default);

    Task<object> GetSurveyAnswersResponseAsync(int surveyId, CancellationToken cancellationToken = default);
}
