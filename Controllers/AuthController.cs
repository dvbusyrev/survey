using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Security;
using MainProject.Services;
using System.Security.Claims;

public class AuthController : Controller
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpGet("")]
    [HttpGet("Auth")]
    [HttpGet("display_auth")]
    [ActionName("display_auth")]
    public IActionResult DisplayAuth()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userRole == AppRoles.Admin)
            {
                return Redirect("/surveys");
            }
            else if (userRole == AppRoles.User && !string.IsNullOrEmpty(userId))
            {
                return Redirect("/my-surveys");
            }
        }

        return View("Auth");
    }

    [AllowAnonymous]
    [ActionName("logout_account")]
    public async Task<IActionResult> LogoutAccount()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("display_auth");
    }

    [AllowAnonymous]
    [HttpPost]
    [ActionName("login")]
    public async Task<IActionResult> Login([FromBody] string[] userData)
    {
        if (userData == null || userData.Length != 2)
            return StatusCode(400, "Неверный формат данных");

        string username = userData[0];
        string password = userData[1];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return StatusCode(400, "Имя пользователя и пароль не могут быть пустыми");

        try
        {
            var loginResult = _authService.Authenticate(username, password);
            if (!loginResult.Success)
            {
                return StatusCode(loginResult.StatusCode, loginResult.ErrorMessage);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, loginResult.UserId.ToString()),
                new Claim(ClaimTypes.Name, loginResult.UserName),
                new Claim(ClaimTypes.Role, loginResult.Role),
                new Claim("organization_name", loginResult.OrganizationName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Json(new
            {
                role = loginResult.Role,
                userId = loginResult.UserId,
                nameUser = loginResult.UserName,
                nameOrganization = loginResult.OrganizationName
            });
        }
        catch
        {
            return StatusCode(500, "Ошибка сервера при попытке авторизации");
        }
    }
}
