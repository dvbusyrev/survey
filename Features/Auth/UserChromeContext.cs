using MainProject.Infrastructure.Security;

namespace MainProject.Application.DTO;

public sealed class UserChromeContext
{
    public int UserId { get; init; }
    public string UserRole { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string OrganizationName { get; init; } = string.Empty;

    public string DisplayName => !string.IsNullOrWhiteSpace(OrganizationName) && !string.IsNullOrWhiteSpace(UserName)
        ? $"{OrganizationName}: {UserName}"
        : (!string.IsNullOrWhiteSpace(UserName) ? UserName : AppRoles.GetDisplayName(UserRole));
}
