using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;

namespace MainProject.Application.UseCases;

public sealed class UserChromeContextService
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

    public async Task<UserChromeContext> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var fallbackContext = BuildFallbackContext();
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return fallbackContext;
        }

        var row = await GetByUserIdAsync(
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

    private async Task<UserChromeContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<UserChromeContext>(new CommandDefinition(
            """
            SELECT
                u.id_user AS UserId,
                u.role AS UserRole,
                u.login AS UserName,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS OrganizationName
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON o.id_organization = u.id_organization
            WHERE u.id_user = @UserId;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
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
