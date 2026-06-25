using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace MainProject.Web.Infrastructure;

public static class ForwardedHeadersConfiguration
{
    public static void Configure(
        ForwardedHeadersOptions options,
        IEnumerable<string> knownProxyAddresses)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var configuredAddress in knownProxyAddresses)
        {
            if (!IPAddress.TryParse(configuredAddress, out var address))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies содержит некорректный IP-адрес: {configuredAddress}.");
            }

            options.KnownProxies.Add(address);
        }
    }
}
