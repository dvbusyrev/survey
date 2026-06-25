using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MainProject.Infrastructure.Health;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        var response = new HealthCheckResponse(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds),
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthCheckEntryResponse(
                    entry.Value.Status.ToString(),
                    Math.Round(entry.Value.Duration.TotalMilliseconds))));

        return context.Response.WriteAsJsonAsync(response, cancellationToken: context.RequestAborted);
    }

    private sealed record HealthCheckResponse(
        string Status,
        double TotalDurationMilliseconds,
        IReadOnlyDictionary<string, HealthCheckEntryResponse> Checks);

    private sealed record HealthCheckEntryResponse(
        string Status,
        double DurationMilliseconds);
}
