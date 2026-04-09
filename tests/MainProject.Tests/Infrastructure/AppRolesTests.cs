using MainProject.Infrastructure.Security;

namespace MainProject.Tests.Infrastructure;

public sealed class AppRolesTests
{
    [Theory]
    [InlineData("admin", AppRoles.Admin)]
    [InlineData("Administrator", AppRoles.Admin)]
    [InlineData("администратор", AppRoles.Admin)]
    [InlineData("пользователь", AppRoles.User)]
    [InlineData("user", AppRoles.User)]
    public void Normalize_MapsKnownAliases(string input, string expected)
    {
        Assert.Equal(expected, AppRoles.Normalize(input));
    }

    [Theory]
    [InlineData(AppRoles.Admin, AppRoles.AdminDisplayName)]
    [InlineData(AppRoles.User, AppRoles.UserDisplayName)]
    [InlineData("custom", "custom")]
    public void GetDisplayName_ReturnsExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, AppRoles.GetDisplayName(input));
    }
}
