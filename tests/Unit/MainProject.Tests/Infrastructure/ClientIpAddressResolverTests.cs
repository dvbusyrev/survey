using System.Net;
using MainProject.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MainProject.Tests.Infrastructure;

public sealed class ClientIpAddressResolverTests
{
    [Fact]
    public void Resolve_UsesRemoteAddress_AndIgnoresUntrustedForwardedHeader()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.24";

        var result = ClientIpAddressResolver.Resolve(context);

        Assert.Equal("192.0.2.10", result);
    }
}
