using Microsoft.Extensions.Hosting;

namespace MainProject.Web.Infrastructure;

public static class ProductionConfigurationValidator
{
    public static void EnsureAllowedHosts(IHostEnvironment environment, string? allowedHosts)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var hosts = allowedHosts?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        if (hosts.Length == 0 || hosts.Any(host => host.Contains('*', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Для production требуется конкретное значение AllowedHosts без wildcard.");
        }
    }
}
