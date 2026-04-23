using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Tests.Infrastructure;

public sealed class DefaultConnectionStringResolverTests
{
    [Fact]
    public void NormalizeForPlatform_ReplacesUnixSocketHostOnWindows()
    {
        var result = DefaultConnectionStringResolver.NormalizeForPlatform(
            "Host=/tmp;Port=5432;Database=survey_recovered;Username=dbusyrev",
            isWindows: true);

        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("survey_recovered", builder.Database);
        Assert.Equal("dbusyrev", builder.Username);
    }

    [Fact]
    public void NormalizeForPlatform_LeavesUnixSocketHostOnNonWindows()
    {
        var result = DefaultConnectionStringResolver.NormalizeForPlatform(
            "Host=/tmp;Port=5432;Database=survey_recovered;Username=dbusyrev",
            isWindows: false);

        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("/tmp", builder.Host);
    }

    [Fact]
    public void NormalizeForPlatform_LeavesTcpHostOnWindows()
    {
        var result = DefaultConnectionStringResolver.NormalizeForPlatform(
            "Host=localhost;Port=5432;Database=survey_recovered;Username=dbusyrev",
            isWindows: true);

        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
    }
}
