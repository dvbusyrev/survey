using System.Diagnostics;
using Dapper;
using Npgsql;

namespace MainProject.Tests.Integration.Database;

public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (!PostgreSqlIntegrationFixture.IsConfigured)
        {
            Skip = "Set SURVEY_TEST_CONNECTION to a dedicated PostgreSQL database whose name contains 'test'.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<PostgreSqlIntegrationFixture>
{
    public const string Name = "PostgreSQL integration";
}

public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private const string ConnectionStringVariable = "SURVEY_TEST_CONNECTION";
    private const string PsqlPathVariable = "SURVEY_TEST_PSQL";

    private readonly string? _connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable(ConnectionStringVariable));

    public Task InitializeAsync()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public NpgsqlConnection CreateConnection()
    {
        EnsureConfigured();
        return new NpgsqlConnection(_connectionString);
    }

    public async Task ResetAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        EnsureConfigured();

        await using (var connection = CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        await RunMigrationsAsync();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException($"{ConnectionStringVariable} is required for PostgreSQL integration tests.");
        }

        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database)
            || !builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} must point to a dedicated database whose name contains 'test'.");
        }
    }

    private async Task RunMigrationsAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString!);
        var processStartInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable(PsqlPathVariable) ?? "psql")
        {
            WorkingDirectory = GetRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        processStartInfo.ArgumentList.Add("-v");
        processStartInfo.ArgumentList.Add("ON_ERROR_STOP=1");
        processStartInfo.ArgumentList.Add("-f");
        processStartInfo.ArgumentList.Add(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));
        SetEnvironmentVariable(processStartInfo, "PGHOST", builder.Host);
        SetEnvironmentVariable(processStartInfo, "PGPORT", builder.Port > 0 ? builder.Port.ToString() : null);
        SetEnvironmentVariable(processStartInfo, "PGDATABASE", builder.Database);
        SetEnvironmentVariable(processStartInfo, "PGUSER", builder.Username);
        SetEnvironmentVariable(processStartInfo, "PGPASSWORD", builder.Password);

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Unable to start psql for integration migrations.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Migrations failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{await standardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{await standardError}");
        }
    }

    private static void SetEnvironmentVariable(ProcessStartInfo startInfo, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            startInfo.Environment[name] = value;
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "main_project.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing main_project.csproj was not found.");
    }
}
