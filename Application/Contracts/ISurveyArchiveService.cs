using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyArchiveService
{
    UserSurveyArchivePageViewModel? GetUserArchivePage(
        int userId,
        int currentPage,
        string? searchTerm,
        string? date,
        string? dateFrom,
        string? dateTo,
        bool signedOnly);
    SurveyArchivePageViewModel GetAdminArchivedSurveysPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        string? surveyIds,
        string? year,
        string? month,
        string? dateFrom,
        string? dateTo);
    IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveys();
    Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request);
}
