using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IOrganizationManagementService
{
    Task<OrganizationListPageViewModel> GetActiveOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false,
        CancellationToken cancellationToken = default);
    Task<OrganizationSurveyAssignmentsPageViewModel> GetOrganizationSurveyAssignmentsPageAsync(CancellationToken cancellationToken = default);
    Task<OrganizationListPageViewModel> GetArchivedOrganizationsPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetArchivedOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationDataResponse>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default);
    Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateOrganizationAsync(OrganizationSaveRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateOrganizationAsync(int id, OrganizationSaveRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ArchiveOrganizationAsync(int id, CancellationToken cancellationToken = default);
    Task<OrganizationSurveyEndDateUpdateResult> UpdateOrganizationSurveyEndDatesAsync(OrganizationSurveyEndDateUpdateRequest request, CancellationToken cancellationToken = default);
}
