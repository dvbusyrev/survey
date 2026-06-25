using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Tests.Integration.Database;

internal sealed class TestNpgsqlConnectionFactory(PostgreSqlIntegrationFixture fixture) : IDbConnectionFactory
{
    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = fixture.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
