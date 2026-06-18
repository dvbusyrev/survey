using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Security;
using MainProject.Application.DTO;
using MainProject.Web.ViewModels;

[Authorize(Roles = AppRoles.Admin)]
public class OrganizationController : Controller
{
    private readonly IOrganizationManagementService _organizationManagementService;

    public OrganizationController(IOrganizationManagementService organizationManagementService)
    {
        _organizationManagementService = organizationManagementService;
    }

    [HttpGet("organizations")]
    [HttpGet("organizations/{variantType}")]
    public IActionResult GetOrganization(
        string? variantType,
        bool openAddOrganizationModal = false,
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null)
    {
        if (string.Equals(variantType, "data", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Json(_organizationManagementService.GetOrganizationOptions());
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Ошибка при получении списка организаций: {ex.Message}" });
            }
        }

        try
        {
            var pageModel = _organizationManagementService.GetActiveOrganizationsPage(
                page,
                sortBy,
                sortDirection,
                openAddOrganizationModal);
            return View("get_organization", pageModel);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
        }
    }

    [HttpPost("organizations/{id:int}/delete")]
    public IActionResult DeleteOrganization(int id)
    {
        try
        {
            var result = _organizationManagementService.ArchiveOrganization(id);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка при удалении организации: {ex.Message}");
        }
    }

    [HttpGet("organizations/create")]
    public IActionResult AddOrganization(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null)
    {
        try
        {
            var pageModel = _organizationManagementService.GetActiveOrganizationsPage(
                page,
                sortBy,
                sortDirection,
                openAddOrganizationModal: true);
            return View("get_organization", pageModel);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при открытии формы добавления организации: {ex.Message}" });
        }
    }

    [HttpGet("organizations/archive")]
    public IActionResult ArchiveListOrganizations(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null)
    {
        try
        {
            return View(
                "archive_list_organizations",
                _organizationManagementService.GetArchivedOrganizationsPage(page, sortBy, sortDirection));
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
        }
    }

    [HttpGet("organizations/survey")]
    [HttpGet("organizations/surveys")]
    public IActionResult OrganizationSurveys()
    {
        try
        {
            return View(
                "organization_surveys",
                _organizationManagementService.GetOrganizationSurveyAssignmentsPage());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении анкет организаций: {ex.Message}" });
        }
    }

    [HttpPost("organizations/create")]
    [ValidateAntiForgeryToken]
    public IActionResult CreateOrganization([FromBody] OrganizationSaveRequest request)
    {
        try
        {
            var result = _organizationManagementService.CreateOrganization(request);
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
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpGet("organizations/{id:int}/edit")]
    public IActionResult UpdateOrganization(int id)
    {
        try
        {
            var organization = _organizationManagementService.GetOrganizationById(id);
            if (organization == null)
            {
                return NotFound("Организация не найдена.");
            }

            return View("update_organization", organization);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных организации: {ex.Message}" });
        }
    }

    [HttpPost("organizations/{id:int}/update")]
    public IActionResult UpdateOrganizationAction(int id, [FromBody] OrganizationSaveRequest request)
    {
        try
        {
            var result = _organizationManagementService.UpdateOrganization(id, request);
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
            return StatusCode(500, $"Ошибка при обновлении организации: {ex.Message}");
        }
    }

    [HttpPost("organizations/survey/end-date")]
    [HttpPost("organizations/surveys/end-date")]
    public IActionResult UpdateOrganizationSurveyEndDates(
        [FromBody] OrganizationSurveyEndDateUpdateRequest? request)
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
            var result = _organizationManagementService.UpdateOrganizationSurveyEndDates(request);
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
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при обновлении даты конца анкет.",
                error = ex.Message
            });
        }
    }
}
