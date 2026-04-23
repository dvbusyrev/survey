using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public static class DefaultConnectionStringResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        return NormalizeForPlatform(connectionString, OperatingSystem.IsWindows());
    }

    public static string NormalizeForPlatform(string connectionString, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));
        }

        if (!isWindows)
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        // Unix socket directories such as /tmp are valid on macOS/Linux, but not on Windows.
        var host = builder.Host;

        if (string.IsNullOrWhiteSpace(host) || !host.StartsWith("/", StringComparison.Ordinal))
        {
            return connectionString;
        }

        builder.Host = "localhost";
        return builder.ConnectionString;
    }
}
