using System.Security.Claims;
using MainProject.Infrastructure.Security;
using MainProject.Application.UseCases;
using Microsoft.AspNetCore.Http;

namespace MainProject.Tests.Services;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void ExposesCurrentUserClaims()
    {
        var service = new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "15"),
                        new Claim(ClaimTypes.Name, "ivanov"),
                        new Claim(ClaimTypes.Role, AppRoles.Admin),
                        new Claim("organization_name", "Тестовая организация")
                    },
                    authenticationType: "TestAuth"))
            }
        });

        Assert.True(service.IsAuthenticated);
        Assert.Equal(15, service.UserId);
        Assert.Equal("ivanov", service.UserName);
        Assert.Equal(AppRoles.Admin, service.Role);
        Assert.Equal("Тестовая организация", service.OrganizationName);
        Assert.True(service.IsAdmin);
    }

    [Fact]
    public void ReturnsSafeDefaults_WhenHttpContextIsMissing()
    {
        var service = new CurrentUserService(new HttpContextAccessor());

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Equal(string.Empty, service.UserName);
        Assert.Equal(string.Empty, service.Role);
        Assert.Equal(string.Empty, service.OrganizationName);
        Assert.False(service.IsAdmin);
    }
}
