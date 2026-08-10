using System.Security.Claims;
using System.Text.Json;
using MainProject.Application.UseCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MainProject.Web.Infrastructure;

public sealed class ApplicationCookieAuthenticationEvents : CookieAuthenticationEvents
{
    public const string BlockedReason = "blocked";
    public const string AuthenticationStatusHeader = "X-Authentication-Status";

    private const string BlockedRequestItem = "Authentication.UserBlocked";
    private readonly IUserAccessStatusService _userAccessStatusService;

    public ApplicationCookieAuthenticationEvents(IUserAccessStatusService userAccessStatusService)
    {
        _userAccessStatusService = userAccessStatusService;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        if (await _userAccessStatusService.IsAccessAllowedAsync(userId, context.HttpContext.RequestAborted))
        {
            return;
        }

        context.HttpContext.Items[BlockedRequestItem] = true;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteAuthenticationResponseAsync(context, StatusCodes.Status401Unauthorized, "Требуется авторизация. Выполните вход снова.");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteAuthenticationResponseAsync(context, StatusCodes.Status403Forbidden, "Доступ запрещён.");

    private static async Task WriteAuthenticationResponseAsync(
        RedirectContext<CookieAuthenticationOptions> context,
        int apiStatusCode,
        string defaultMessage)
    {
        var isBlocked = context.HttpContext.Items.ContainsKey(BlockedRequestItem);
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = apiStatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            if (isBlocked)
            {
                context.Response.Headers[AuthenticationStatusHeader] = BlockedReason;
            }

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = isBlocked ? AuthService.BlockedUserMessage : defaultMessage
            }));
            return;
        }

        context.Response.Redirect(isBlocked ? $"/?auth={BlockedReason}" : "/");
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
            string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
