using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyArchiveService
{
    Task<UserSurveyArchivePageViewModel?> GetUserArchivePageAsync(
        int userId,
        int currentPage,
        string? searchTerm,
        string? date,
        string? dateFrom,
        string? dateTo,
        bool signedOnly,
        CancellationToken cancellationToken = default);
    Task<SurveyArchivePageViewModel> GetAdminArchivedSurveysPageAsync(
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
    Task<IReadOnlyList<ArchivedSurvey>> GetAdminArchivedSurveysAsync(CancellationToken cancellationToken = default);
    Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request, CancellationToken cancellationToken = default);
}
