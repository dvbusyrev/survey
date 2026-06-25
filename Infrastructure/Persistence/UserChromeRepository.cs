using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;

namespace MainProject.Infrastructure.Persistence;

public sealed class UserChromeRepository : IUserChromeRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserChromeRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserChromeContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
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
}
