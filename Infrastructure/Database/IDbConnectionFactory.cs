using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
