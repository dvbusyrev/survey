using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Security;
using MainProject.Application.DTO;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize(Roles = AppRoles.Admin)]
public class OrganizationController : Controller
{
    private readonly OrganizationManagementService _organizationManagementService;

    public OrganizationController(OrganizationManagementService organizationManagementService)
    {
        _organizationManagementService = organizationManagementService;
    }

    [HttpGet("organizations")]
    [HttpGet("organizations/{variantType}")]
    public async Task<IActionResult> GetOrganization(
        string? variantType,
        bool openAddOrganizationModal = false,
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(variantType, "data", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Json(await _organizationManagementService.GetOrganizationOptionsAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                return this.SafeError(ex, "Не удалось загрузить список организаций.", "Ошибка при получении списка организаций");
            }
        }

        try
        {
            var pageModel = await _organizationManagementService.GetActiveOrganizationsPageAsync(
                page,
                sortBy,
                sortDirection,
                openAddOrganizationModal,
                cancellationToken);
            return View("get_organization", pageModel);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить список организаций.", "Ошибка при получении списка организаций");
        }
    }

    [HttpPost("organizations/{id:int}/delete")]
    public async Task<IActionResult> DeleteOrganization(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _organizationManagementService.ArchiveOrganizationAsync(id, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Message);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось удалить организацию.", "Ошибка при удалении организации");
        }
    }

    [HttpGet("organizations/create")]
    public async Task<IActionResult> AddOrganization(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pageModel = await _organizationManagementService.GetActiveOrganizationsPageAsync(
                page,
                sortBy,
                sortDirection,
                openAddOrganizationModal: true,
                cancellationToken: cancellationToken);
            return View("get_organization", pageModel);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось открыть форму добавления организации.", "Ошибка при открытии формы добавления организации");
        }
    }

    [HttpGet("organizations/archive")]
    public async Task<IActionResult> ArchiveListOrganizations(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View(
                "archive_list_organizations",
                await _organizationManagementService.GetArchivedOrganizationsPageAsync(page, sortBy, sortDirection, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить список организаций.", "Ошибка при получении архивных организаций");
        }
    }

    [HttpGet("organizations/survey")]
    [HttpGet("organizations/surveys")]
    public async Task<IActionResult> OrganizationSurveys(CancellationToken cancellationToken)
    {
        try
        {
            return View(
                "organization_surveys",
                await _organizationManagementService.GetOrganizationSurveyAssignmentsPageAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить продление анкет.", "Ошибка при получении анкет организаций");
        }
    }

    [HttpPost("organizations/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrganization([FromBody] OrganizationSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _organizationManagementService.CreateOrganizationAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    error = result.Error
                });
            }

            return Json(new
            {
                success = true,
                message = result.Message,
                organizationId = result.EntityId,
                shouldReload = result.ShouldReload
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать организацию.", "Ошибка при создании организации");
        }
    }

    [HttpGet("organizations/{id:int}/edit")]
    public async Task<IActionResult> UpdateOrganization(int id, CancellationToken cancellationToken)
    {
        try
        {
            var organization = await _organizationManagementService.GetOrganizationByIdAsync(id, cancellationToken);
            if (organization == null)
            {
                return NotFound("Организация не найдена.");
            }

            return View("update_organization", organization);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить данные организации.", "Ошибка при получении данных организации");
        }
    }

    [HttpPost("organizations/{id:int}/update")]
    public async Task<IActionResult> UpdateOrganizationAction(int id, [FromBody] OrganizationSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _organizationManagementService.UpdateOrganizationAsync(id, request, cancellationToken);
            if (!result.Success)
            {
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    return BadRequest(result.Message);
                }

                return NotFound(result.Message);
            }

            return Ok(result.Message);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось обновить организацию.", "Ошибка при обновлении организации");
        }
    }

    [HttpPost("organizations/survey/end-date")]
    [HttpPost("organizations/surveys/end-date")]
    public async Task<IActionResult> UpdateOrganizationSurveyEndDates(
        [FromBody] OrganizationSurveyEndDateUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Не удалось обновить дату конца анкет.",
                errors = new[] { "Не переданы данные для сохранения." }
            });
        }

        try
        {
            var result = await _organizationManagementService.UpdateOrganizationSurveyEndDatesAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    error = result.Error,
                    errors = result.Errors
                });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                updatedAssignments = result.UpdatedAssignments
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось обновить дату конца анкет.", "Ошибка при обновлении даты конца анкет");
        }
    }
}
