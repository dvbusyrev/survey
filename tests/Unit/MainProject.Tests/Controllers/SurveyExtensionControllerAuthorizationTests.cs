using System.Reflection;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace MainProject.Tests.Controllers;

public sealed class SurveyExtensionControllerAuthorizationTests
{
    [Fact]
    public void SurveyExtensionController_RequiresAdministratorRole()
    {
        var attribute = Assert.Single(
            typeof(SurveyExtensionController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal(AppRoles.Admin, attribute.Roles);
    }
}
