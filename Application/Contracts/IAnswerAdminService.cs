using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAnswerAdminService
{
    AnswerListPageViewModel GetAnswersPage();
    SurveySignaturePageViewModel GetSignaturePage(int surveyId);
    AnswerStatisticsResponse GetStatistics();
}
