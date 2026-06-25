using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO.Read;

namespace MainProject.Infrastructure.Persistence;

public sealed class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AuthUserRecord?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<AuthUserRecord>(new CommandDefinition(
            """
            SELECT
                u.id_user AS UserId,
                u.role AS Role,
                u.login AS UserName,
                COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name, '') AS OrganizationName,
                u.password AS PasswordHash
            FROM public.app_user u
            LEFT JOIN public.organization o
                ON u.id_organization = o.id_organization
            WHERE u.login = @Login;
            """,
            new { Login = login },
            cancellationToken: cancellationToken));
    }

    public async Task UpdatePasswordHashAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.app_user
            SET password = @PasswordHash
            WHERE id_user = @UserId;
            """,
            new { UserId = userId, PasswordHash = passwordHash },
            cancellationToken: cancellationToken));
    }
}
