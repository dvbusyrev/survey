using Npgsql;
using main_project.Services;

namespace main_project.Infrastructure.Database;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly CurrentUserService _currentUserService;

    public NpgsqlConnectionFactory(IConfiguration configuration, CurrentUserService currentUserService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection");
        _currentUserService = currentUserService;
    }

    public NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        DatabaseSchemaBootstrapper.EnsureInitialized(connection);
        ApplySessionAuditContext(connection);
        return connection;
    }

    private void ApplySessionAuditContext(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @userId, false);",
            connection);

        command.Parameters.AddWithValue("@userId", _currentUserService.UserId?.ToString() ?? string.Empty);
        command.ExecuteNonQuery();
    }
}
