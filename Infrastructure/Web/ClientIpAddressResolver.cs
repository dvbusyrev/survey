using Microsoft.AspNetCore.Http;

namespace MainProject.Web.Infrastructure;

public static class ClientIpAddressResolver
{
    public static string Resolve(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
