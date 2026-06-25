using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyAdminService
{
    Task<IReadOnlyList<Survey>> GetSurveysAsync(CancellationToken cancellationToken = default);
    Task<SurveyListPageViewModel> GetSurveysPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        string? organizationIds,
        CancellationToken cancellationToken = default);
    Task<SurveyCommandResult> CreateSurveyAsync(SurveyAddRequest? request, CancellationToken cancellationToken = default);
    Task<SurveyEditPageViewModel?> GetSurveyEditPageAsync(int id, CancellationToken cancellationToken = default);
    Task<SurveyCommandResult> UpdateSurveyAsync(int id, SurveyUpdateRequest? model, CancellationToken cancellationToken = default);
    Task<SurveyCommandResult> UpdateActiveSurveysWorkPeriodAsync(SurveyWorkPeriodRequest? request, CancellationToken cancellationToken = default);
    Task<Survey?> GetSurveyForCopyAsync(int id, CancellationToken cancellationToken = default);
    Task<SurveyCommandResult> CopySurveyAsync(int id, SurveyCopyRequest? request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Survey>?> DeleteSurveyAsync(int surveyId, CancellationToken cancellationToken = default);
}
