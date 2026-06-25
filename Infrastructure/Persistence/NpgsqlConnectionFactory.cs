using Npgsql;
using MainProject.Application.Contracts;

namespace MainProject.Infrastructure.Persistence;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ICurrentUserService _currentUserService;

    public NpgsqlConnectionFactory(IConfiguration configuration, ICurrentUserService currentUserService)
    {
        _connectionString = DefaultConnectionStringResolver.Resolve(configuration);
        _currentUserService = currentUserService;
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ApplySessionAuditContextAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task ApplySessionAuditContextAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @userId, false);",
            connection);

        command.Parameters.AddWithValue("@userId", _currentUserService.UserId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
