using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.Contracts;

public interface IOrganizationManagementService
{
    OrganizationListPageViewModel GetActiveOrganizationsPage(bool openAddOrganizationModal = false);
    IReadOnlyList<Organization> GetArchivedOrganizations();
    IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions();
    Organization? GetOrganizationById(int id);
    OperationResult CreateOrganization(OrganizationSaveRequest request);
    OperationResult UpdateOrganization(int id, OrganizationSaveRequest request);
    OperationResult ArchiveOrganization(int id);
}
