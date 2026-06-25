using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IAnswerAdminService
{
    Task<AnswerListPageViewModel> GetAnswersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default);
    Task<SurveySignaturePageViewModel> GetSignaturePageAsync(int surveyId, CancellationToken cancellationToken = default);
    Task<AnswerStatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
