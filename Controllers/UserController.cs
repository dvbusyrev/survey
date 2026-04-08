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

    [ActionName("get_users")]
    public IActionResult GetUsers()
    {
        try
        {
            return View(_userManagementService.GetActiveUsersPage());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }

    [ActionName("add_user")]
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

    [ActionName("update_user")]
    public IActionResult UpdateUser(int id)
    {
        try
        {
            var user = _userManagementService.GetUserById(id);
            if (user == null)
            {
                return NotFound("Пользователь не найден.");
            }

            return View(user);
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных пользователя: {ex.Message}" });
        }
    }

    [HttpPost]
    [ActionName("add_user_bd")]
    public IActionResult AddUserBd([FromBody] UserSaveRequest request)
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

    [ActionName("update_user_bd")]
    public IActionResult UpdateUserBd(int id, [FromBody] UserUpdateRequest request)
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

    [HttpPost]
    [ActionName("delete_user")]
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

    [ActionName("archive_list_users")]
    public IActionResult ArchiveListUsers()
    {
        try
        {
            return View(_userManagementService.GetArchivedUsers());
        }
        catch (Exception ex)
        {
            return View("Error", new ErrorViewModel { Message = $"Ошибка при получении пользователей: {ex.Message}" });
        }
    }
}
