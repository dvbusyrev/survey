using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
using MainProject.Web.ViewModels;

[Authorize(Roles = AppRoles.Admin)]
public class UserController : Controller
{
    private readonly UserManagementService _userManagementService;

    public UserController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View("get_users", await _userManagementService.GetActiveUsersPageAsync(page, sortBy, sortDirection, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить список пользователей.", "Ошибка при получении пользователей");
        }
    }

    [HttpGet("users/create")]
    public async Task<IActionResult> AddUser(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View(
                "get_users",
                await _userManagementService.GetActiveUsersPageAsync(page, sortBy, sortDirection, openAddUserModal: true, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось открыть форму добавления пользователя.", "Ошибка при открытии формы добавления пользователя");
        }
    }

    [HttpGet("users/{id:int}/edit")]
    public async Task<IActionResult> UpdateUser(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManagementService.GetUserByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound("Клиент не найден.");
            }

            return View("update_user", user);
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить данные пользователя.", "Ошибка при получении данных пользователя");
        }
    }

    [HttpPost("users/create")]
    public async Task<IActionResult> CreateUser([FromBody] UserSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _userManagementService.CreateUserAsync(request, cancellationToken);
            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось создать пользователя.", "Ошибка при создании пользователя");
        }
    }

    [HttpPost("users/{id:int}/update")]
    public async Task<IActionResult> UpdateUserAction(int id, [FromBody] UserUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _userManagementService.UpdateUserAsync(id, request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Json(new
            {
                success = true,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось обновить пользователя.", "Ошибка при обновлении пользователя");
        }
    }

    [HttpPost("users/{id:int}/delete")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _userManagementService.DeleteUserAsync(id, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Message);
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось удалить пользователя.", "Ошибка при удалении пользователя");
        }
    }

    [HttpGet("users/archive")]
    public async Task<IActionResult> ArchiveListUsers(
        int page = 1,
        string? sortBy = null,
        string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return View("archived_users", await _userManagementService.GetArchivedUsersPageAsync(page, sortBy, sortDirection, cancellationToken));
        }
        catch (Exception ex)
        {
            return this.SafeErrorView(ex, "Не удалось загрузить список пользователей.", "Ошибка при получении архивных пользователей");
        }
    }
}
