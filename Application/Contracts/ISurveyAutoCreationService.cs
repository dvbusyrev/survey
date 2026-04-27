using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface ISurveyAutoCreationService
{
    Task<SurveyAutoCreationPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurveySelectionItem>> GetSurveyOptionsAsync(CancellationToken cancellationToken = default);
    Task<SurveyAutoCreationCommandResult> SaveAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default);
    Task<SurveyAutoCreationCommandResult> StartAsync(SurveyAutoCreationSettingsRequest? request, CancellationToken cancellationToken = default);
    Task<SurveyAutoCreationCommandResult> StopAsync(CancellationToken cancellationToken = default);
    Task<SurveyAutoCreationRunResult> RunPendingAsync(CancellationToken cancellationToken = default);
}
