using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Tests.Integration.Database;

internal sealed class TestNpgsqlConnectionFactory(PostgreSqlIntegrationFixture fixture) : IDbConnectionFactory
{
    public NpgsqlConnection CreateConnection()
    {
        var connection = fixture.CreateConnection();
        connection.Open();
        return connection;
    }
}
