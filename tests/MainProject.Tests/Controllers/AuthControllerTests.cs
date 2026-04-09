using System.Reflection;
using System.Security.Claims;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MainProject.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public void DisplayAuth_ReturnsAuthView_ForAnonymousUser()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        var result = controller.DisplayAuth();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Auth", viewResult.ViewName);
    }

    [Fact]
    public void DisplayAuth_RedirectsAdmin_ToSurveys()
    {
        var controller = CreateController(CreatePrincipal(
            (ClaimTypes.Role, AppRoles.Admin),
            (ClaimTypes.NameIdentifier, "42")));

        var result = controller.DisplayAuth();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/surveys", redirectResult.Url);
    }

    [Fact]
    public void DisplayAuth_RedirectsUser_ToMySurveys()
    {
        var controller = CreateController(CreatePrincipal(
            (ClaimTypes.Role, AppRoles.User),
            (ClaimTypes.NameIdentifier, "7")));

        var result = controller.DisplayAuth();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/my-surveys", redirectResult.Url);
    }

    [Fact]
    public void Login_HasRateLimitingPolicy()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login), BindingFlags.Instance | BindingFlags.Public);

        var attribute = Assert.Single(method!.GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Equal("login-attempts", attribute.PolicyName);
    }

    private static AuthController CreateController(ClaimsPrincipal principal)
    {
        return new AuthController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "TestAuth");

        return new ClaimsPrincipal(identity);
    }
}
