using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IOrganizationManagementService
{
    OrganizationListPageViewModel GetActiveOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddOrganizationModal = false);
    OrganizationSurveyAssignmentsPageViewModel GetOrganizationSurveyAssignmentsPage();
    OrganizationListPageViewModel GetArchivedOrganizationsPage(
        int currentPage,
        string? sortBy,
        string? sortDirection);
    IReadOnlyList<Organization> GetArchivedOrganizations();
    IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions();
    Organization? GetOrganizationById(int id);
    OperationResult CreateOrganization(OrganizationSaveRequest request);
    OperationResult UpdateOrganization(int id, OrganizationSaveRequest request);
    OperationResult ArchiveOrganization(int id);
    OrganizationSurveyEndDateUpdateResult UpdateOrganizationSurveyEndDates(OrganizationSurveyEndDateUpdateRequest request);
}
