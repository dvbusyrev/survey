using System.Text.Json;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MainProject.Tests.Controllers;

public sealed class OrganizationControllerTests
{
    [Fact]
    public async Task DeleteOrganization_ReturnsOk_WhenDeletionSucceeds()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = true,
                Message = "Организация успешно удалена."
            }));

        var result = await controller.DeleteOrganization(42, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.SerializeToElement(okResult.Value);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal("Организация успешно удалена.", payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsConflict_WhenArchiveIsForbidden()
    {
        var controller = new OrganizationController(new StubOrganizationManagementService(
            new OperationResult
            {
                Success = false,
                Message = "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
                Code = "organization_in_use"
            }));

        var result = await controller.DeleteOrganization(42, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var payload = JsonSerializer.SerializeToElement(conflictResult.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.",
            payload.GetProperty("message").GetString());
    }

    private sealed class StubOrganizationManagementService : OrganizationManagementService
    {
        private readonly OperationResult _deletionResult;

        public StubOrganizationManagementService(OperationResult deletionResult)
        {
            _deletionResult = deletionResult;
        }

        public override Task<OrganizationListPageViewModel> GetActiveOrganizationsPageAsync(
            int currentPage,
            string? sortBy,
            string? sortDirection,
            bool openAddOrganizationModal = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationListPageViewModel());

        public override Task<OrganizationListPageViewModel> GetArchivedOrganizationsPageAsync(
            int currentPage,
            string? sortBy,
            string? sortDirection,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationListPageViewModel());

        public override Task<OrganizationSurveyAssignmentsPageViewModel> GetOrganizationSurveyAssignmentsPageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationSurveyAssignmentsPageViewModel());

        public override Task<IReadOnlyList<Organization>> GetArchivedOrganizationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Organization>>(Array.Empty<Organization>());

        public override Task<IReadOnlyList<OrganizationDataResponse>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrganizationDataResponse>>(Array.Empty<OrganizationDataResponse>());

        public override Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Organization?>(null);

        public override Task<OperationResult> CreateOrganizationAsync(OrganizationSaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<OperationResult> UpdateOrganizationAsync(int id, OrganizationSaveRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<OperationResult> DeleteOrganizationAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_deletionResult);

        public override Task<OrganizationSurveyEndDateUpdateResult> UpdateOrganizationSurveyEndDatesAsync(OrganizationSurveyEndDateUpdateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
