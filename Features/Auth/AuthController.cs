using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.DTO;
using MainProject.Application.UseCases;
using MainProject.Infrastructure.Security;
using MainProject.Web.Infrastructure;
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
    [HttpGet("auth")]
    public IActionResult DisplayAuth()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userRole == AppRoles.Admin)
            {
                return Redirect("/survey");
            }
            else if (userRole == AppRoles.User && !string.IsNullOrEmpty(userId))
            {
                return Redirect("/survey");
            }
        }

        return View("Auth");
    }

    [AllowAnonymous]
    [HttpPost("auth/logout")]
    public async Task<IActionResult> LogoutAccount()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] string[] userData, CancellationToken cancellationToken)
    {
        if (userData == null || userData.Length != 2)
            return StatusCode(400, "Некорректные данные для входа.");

        string username = userData[0];
        string password = userData[1];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return StatusCode(400, "Введите логин и пароль.");

        try
        {
            var loginResult = await AuthenticateAndSignInAsync(username, password, cancellationToken);
            if (!loginResult.Success)
            {
                return StatusCode(loginResult.StatusCode, loginResult.ErrorMessage);
            }

            return Json(new
            {
                role = loginResult.Role,
                userId = loginResult.UserId,
                nameUser = loginResult.UserName,
                nameOrganization = loginResult.OrganizationName
            });
        }
        catch (Exception ex)
        {
            return this.SafeError(ex, "Не удалось выполнить вход.", "Ошибка при попытке авторизации");
        }
    }

    [AllowAnonymous]
    [HttpPost("auth/login-form")]
    public async Task<IActionResult> LoginForm(
        [FromForm] string username,
        [FromForm] string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Redirect("/?auth=missing");
        }

        try
        {
            var loginResult = await AuthenticateAndSignInAsync(username, password, cancellationToken);
            if (loginResult.Success)
            {
                return Redirect("/survey");
            }

            var status = loginResult.StatusCode switch
            {
                StatusCodes.Status403Forbidden => "blocked",
                StatusCodes.Status401Unauthorized => "invalid",
                _ => "error"
            };

            return Redirect($"/?auth={status}");
        }
        catch (Exception ex)
        {
            _ = this.SafeError(ex, "Не удалось выполнить вход.", "Ошибка при попытке авторизации");
            return Redirect("/?auth=error");
        }
    }

    private async Task<LoginResult> AuthenticateAndSignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var loginResult = await _authService.AuthenticateAsync(username, password, cancellationToken);
        if (!loginResult.Success)
        {
            return loginResult;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, loginResult.UserId.ToString()),
            new(ClaimTypes.Name, loginResult.UserName),
            new(ClaimTypes.Role, loginResult.Role),
            new("organization_name", loginResult.OrganizationName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true
            });

        return loginResult;
    }
}
