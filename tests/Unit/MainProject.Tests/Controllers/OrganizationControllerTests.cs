using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MainProject.Tests.Controllers;

public sealed class OrganizationControllerTests
{
    [Fact]
    public void DeleteOrganization_ReturnsOk_WhenArchiveSucceeds()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = true,
                Message = "Организация успешно удалена."
            }));

        var result = controller.DeleteOrganization(42);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Организация успешно удалена.", okResult.Value);
    }

    [Fact]
    public void DeleteOrganization_ReturnsBadRequest_WhenArchiveIsForbidden()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = false,
                Message = "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи."
            }));

        var result = controller.DeleteOrganization(42);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
            badRequestResult.Value);
    }

    private sealed class StubOrganizationManagementService : IOrganizationManagementService
    {
        private readonly OperationResult _archiveResult;

        public StubOrganizationManagementService(OperationResult archiveResult)
        {
            _archiveResult = archiveResult;
        }

        public OrganizationListPageViewModel GetActiveOrganizationsPage(bool openAddOrganizationModal = false)
            => new();

        public OrganizationSurveyAssignmentsPageViewModel GetOrganizationSurveyAssignmentsPage()
            => new();

        public IReadOnlyList<Organization> GetArchivedOrganizations()
            => Array.Empty<Organization>();

        public IReadOnlyList<OrganizationDataResponse> GetOrganizationOptions()
            => Array.Empty<OrganizationDataResponse>();

        public Organization? GetOrganizationById(int id)
            => null;

        public OperationResult CreateOrganization(OrganizationSaveRequest request)
            => throw new NotSupportedException();

        public OperationResult UpdateOrganization(int id, OrganizationSaveRequest request)
            => throw new NotSupportedException();

        public OperationResult ArchiveOrganization(int id)
            => _archiveResult;

        public OrganizationSurveyEndDateUpdateResult UpdateOrganizationSurveyEndDates(OrganizationSurveyEndDateUpdateRequest request)
            => throw new NotSupportedException();
    }
}
