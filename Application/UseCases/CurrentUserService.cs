using System.Security.Claims;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Security;

namespace MainProject.Application.UseCases;

public class CurrentUserService : ICurrentUserService
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
