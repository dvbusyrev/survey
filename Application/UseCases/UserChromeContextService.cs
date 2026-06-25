using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Security;

namespace MainProject.Application.UseCases;

public sealed class UserChromeContextService : IUserChromeContextService
{
    private readonly IUserChromeRepository _userChromeRepository;
    private readonly ICurrentUserService _currentUserService;

    public UserChromeContextService(
        IUserChromeRepository userChromeRepository,
        ICurrentUserService currentUserService)
    {
        _userChromeRepository = userChromeRepository;
        _currentUserService = currentUserService;
    }

    public async Task<UserChromeContext> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var fallbackContext = BuildFallbackContext();
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return fallbackContext;
        }

        var row = await _userChromeRepository.GetByUserIdAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (row == null)
        {
            return fallbackContext;
        }

        return new UserChromeContext
        {
            UserId = row.UserId,
            UserRole = AppRoles.Normalize(row.UserRole),
            UserName = row.UserName ?? string.Empty,
            OrganizationName = row.OrganizationName ?? string.Empty
        };
    }

    private UserChromeContext BuildFallbackContext()
    {
        return new UserChromeContext
        {
            UserId = _currentUserService.UserId ?? 0,
            UserRole = AppRoles.Normalize(_currentUserService.Role),
            UserName = _currentUserService.UserName,
            OrganizationName = _currentUserService.OrganizationName
        };
    }
}
