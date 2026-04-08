using System.Security.Claims;
using main_project.Infrastructure.Security;

namespace main_project.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string UserName => User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    public string Role => User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    public string OrganizationName => User?.FindFirst("organization_name")?.Value ?? string.Empty;
    public bool IsAdmin => User?.IsInRole(AppRoles.Admin) ?? false;
}
