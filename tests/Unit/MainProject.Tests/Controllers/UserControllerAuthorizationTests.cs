using System.Reflection;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace MainProject.Tests.Controllers;

public sealed class UserControllerAuthorizationTests
{
    [Fact]
    public void UserController_RequiresAdministratorRole()
    {
        var attribute = Assert.Single(
            typeof(UserController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal(AppRoles.Admin, attribute.Roles);
    }
}
