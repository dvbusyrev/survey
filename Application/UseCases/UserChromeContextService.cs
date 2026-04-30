using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;

namespace MainProject.Application.UseCases;

public sealed class UserChromeContextService : IUserChromeContextService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUserService;

    public UserChromeContextService(
        IDbConnectionFactory connectionFactory,
        ICurrentUserService currentUserService)
    {
        _connectionFactory = connectionFactory;
        _currentUserService = currentUserService;
    }

    public UserChromeContext GetCurrentContext()
    {
        var fallbackContext = BuildFallbackContext();
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return fallbackContext;
        }

        using var connection = _connectionFactory.CreateConnection();
        var row = connection.QueryFirstOrDefault<UserChromeContextRow>(
            """
            SELECT
                u.id_user AS UserId,
                u.role AS UserRole,
                u.login AS UserName,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS OrganizationName
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON o.id_organization = u.id_organization
            WHERE u.id_user = @userId;
            """,
            new { userId = _currentUserService.UserId.Value });

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

    private sealed class UserChromeContextRow
    {
        public int UserId { get; init; }
        public string UserRole { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string OrganizationName { get; init; } = string.Empty;
    }
}
