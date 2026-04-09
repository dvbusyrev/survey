using Npgsql;
using MainProject.Application.Contracts;

namespace MainProject.Infrastructure.Persistence;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ICurrentUserService _currentUserService;

    public NpgsqlConnectionFactory(IConfiguration configuration, ICurrentUserService currentUserService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection");
        _currentUserService = currentUserService;
    }

    public NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
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
