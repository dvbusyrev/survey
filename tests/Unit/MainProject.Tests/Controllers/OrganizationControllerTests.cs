using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MainProject.Tests.Controllers;

public sealed class OrganizationControllerTests
{
    [Fact]
    public async Task DeleteOrganization_ReturnsOk_WhenArchiveSucceeds()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = true,
                Message = "Организация успешно удалена."
            }));

        var result = await controller.DeleteOrganization(42, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Организация успешно удалена.", okResult.Value);
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsBadRequest_WhenArchiveIsForbidden()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = false,
                Message = "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи."
            }));

        var result = await controller.DeleteOrganization(42, CancellationToken.None);

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

        public Task<OrganizationListPageViewModel> GetActiveOrganizationsPageAsync(
            int currentPage,
            string? sortBy,
            string? sortDirection,
            bool openAddOrganizationModal = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationListPageViewModel());

        public Task<OrganizationListPageViewModel> GetArchivedOrganizationsPageAsync(
            int currentPage,
            string? sortBy,
            string? sortDirection,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationListPageViewModel());

        public Task<OrganizationSurveyAssignmentsPageViewModel> GetOrganizationSurveyAssignmentsPageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationSurveyAssignmentsPageViewModel());

        public Task<IReadOnlyList<Organization>> GetArchivedOrganizationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Organization>>(Array.Empty<Organization>());

        public Task<IReadOnlyList<OrganizationDataResponse>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrganizationDataResponse>>(Array.Empty<OrganizationDataResponse>());

        public Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Organization?>(null);

        public Task<OperationResult> CreateOrganizationAsync(OrganizationSaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OperationResult> UpdateOrganizationAsync(int id, OrganizationSaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OperationResult> ArchiveOrganizationAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_archiveResult);

        public Task<OrganizationSurveyEndDateUpdateResult> UpdateOrganizationSurveyEndDatesAsync(OrganizationSurveyEndDateUpdateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
