using Dapper;
using MainProject.Application.Contracts;
using MainProject.Infrastructure.Persistence;

namespace MainProject.Application.UseCases;

public interface IUserAccessStatusService
{
    Task<bool> IsAccessAllowedAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class UserAccessStatusService : IUserAccessStatusService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IClock _clock;

    public UserAccessStatusService(IDbConnectionFactory connectionFactory, IClock clock)
    {
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    public async Task<bool> IsAccessAllowedAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.app_user u
                INNER JOIN public.organization o
                    ON o.id_organization = u.id_organization
                WHERE u.id_user = @UserId
                  AND (u.date_end IS NULL OR u.date_end >= @Today)
                  AND (o.date_end IS NULL OR o.date_end >= @Today)
            );
            """,
            new
            {
                UserId = userId,
                Today = _clock.Today.Date
            },
            cancellationToken: cancellationToken));
    }
}
