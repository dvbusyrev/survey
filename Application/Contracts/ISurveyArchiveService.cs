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
    IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveys();
    Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request);
}
