using Microsoft.AspNetCore.Mvc;
using MainProject.Models;
using MainProject.Services.Admin;

public class UserController : Controller
{
    private readonly UserManagementService _userManagementService;

    public UserController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        try
        {
            return View("get_users", _userManagementService.GetActiveUsersPage());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }

    [HttpGet("users/create")]
    public IActionResult AddUser()
    {
        try
        {
            return View("get_users", _userManagementService.GetActiveUsersPage(openAddUserModal: true));
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при открытии формы добавления пользователя: {ex.Message}" });
        }
    }

    [HttpGet("users/{id:int}/edit")]
    public IActionResult UpdateUser(int id)
    {
        try
        {
            var user = _userManagementService.GetUserById(id);
            if (user == null)
            {
                return NotFound("Пользователь не найден.");
            }

            return View("update_user", user);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных пользователя: {ex.Message}" });
        }
    }

    [HttpPost("users/create")]
    public IActionResult CreateUser([FromBody] UserSaveRequest request)
    {
        try
        {
            var result = _userManagementService.CreateUser(request);
            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = $"Серверная ошибка: {ex.Message}"
            });
        }
    }

    [HttpPost("users/{id:int}/update")]
    public IActionResult UpdateUserAction(int id, [FromBody] UserUpdateRequest request)
    {
        try
        {
            var result = _userManagementService.UpdateUser(id, request);
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
            return StatusCode(500, new
            {
                success = false,
                message = $"Ошибка при обновлении: {ex.Message}"
            });
        }
    }

    [HttpPost("users/{id:int}/delete")]
    public IActionResult DeleteUser(int id)
    {
        try
        {
            var result = _userManagementService.DeleteUser(id);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка при удалении пользователя: {ex.Message}");
        }
    }

    [HttpGet("users/archive")]
    public IActionResult ArchiveListUsers()
    {
        try
        {
            return View("archive_list_users", _userManagementService.GetArchivedUsers());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }
}
