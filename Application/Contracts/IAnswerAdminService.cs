using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAnswerAdminService
{
    AnswerListPageViewModel GetAnswersPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo);
    SurveySignaturePageViewModel GetSignaturePage(int surveyId);
    AnswerStatisticsResponse GetStatistics();
}
