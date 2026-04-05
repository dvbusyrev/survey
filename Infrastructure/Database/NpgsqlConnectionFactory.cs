using Npgsql;

namespace main_project.Infrastructure.Database;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection");
    }

    public NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        DatabaseSchemaBootstrapper.EnsureInitialized(connection);
        return connection;
    }
}
