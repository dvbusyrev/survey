using System.Net;
using MainProject.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace MainProject.Tests.Infrastructure;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void Configure_TrustsOnlyExplicitlyConfiguredProxies()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfiguration.Configure(options, ["10.0.0.10"]);

        Assert.Empty(options.KnownNetworks);
        Assert.Single(options.KnownProxies);
        Assert.Contains(IPAddress.Parse("10.0.0.10"), options.KnownProxies);
        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
    }

    [Fact]
    public void Configure_RejectsInvalidProxyAddress()
    {
        var options = new ForwardedHeadersOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ForwardedHeadersConfiguration.Configure(options, ["not-an-ip"]));

        Assert.Contains("ReverseProxy:KnownProxies", exception.Message);
    }
}
