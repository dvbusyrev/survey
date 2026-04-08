using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Security;
using MainProject.Models;
using MainProject.Services.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class OrganizationController : Controller
{
    private readonly OrganizationManagementService _organizationManagementService;

    public OrganizationController(OrganizationManagementService organizationManagementService)
    {
        _organizationManagementService = organizationManagementService;
    }

    [ActionName("get_organization")]
    public IActionResult GetOrganization(string? variantType, bool openAddOrganizationModal = false)
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
            var pageModel = _organizationManagementService.GetActiveOrganizationsPage(openAddOrganizationModal);
            return View(pageModel);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
        }
    }

    [HttpPost]
    [ActionName("delete_organization")]
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

    [ActionName("add_organization")]
    public IActionResult AddOrganization()
    {
        return Redirect("/organizations?openAddOrganizationModal=true");
    }

    [ActionName("archive_list_organizations")]
    public IActionResult ArchiveListOrganizations()
    {
        try
        {
            return View(_organizationManagementService.GetArchivedOrganizations());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("add_organization_bd")]
    public IActionResult AddOrganizationBd([FromBody] OrganizationSaveRequest request)
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

    [ActionName("update_organization")]
    public IActionResult UpdateOrganization(int id)
    {
        try
        {
            var organization = _organizationManagementService.GetOrganizationById(id);
            if (organization == null)
            {
                return NotFound("Организация не найдена.");
            }

            return View(organization);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных организации: {ex.Message}" });
        }
    }

    [HttpPost("update_organization_bd/{id}")]
    [ActionName("update_organization_bd")]
    public IActionResult UpdateOrganizationBd(int id, [FromBody] OrganizationSaveRequest request)
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
}
