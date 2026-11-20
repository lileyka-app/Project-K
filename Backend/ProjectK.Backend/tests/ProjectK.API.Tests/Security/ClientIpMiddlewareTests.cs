using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProjectK.API.Hosting;
using ProjectK.API.Middleware;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// SEC-4.1: the address a request is charged to comes from the one header the proxy in front
/// writes, never from what the caller can forge.
/// </summary>
public class ClientIpMiddlewareTests
{
    private static readonly IPAddress ConnectionAddress = IPAddress.Parse("203.0.113.10");

    [Fact]
    public async Task InvokeAsync_ShouldTakeTheConfiguredHeader_OverTheConnectionAddress()
    {
        var context = Context(header: "CF-Connecting-IP", value: "198.51.100.7");

        await Middleware("CF-Connecting-IP").InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("198.51.100.7"), context.Connection.RemoteIpAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("<script>")]
    public async Task InvokeAsync_ShouldKeepTheConnectionAddress_WhenTheHeaderIsNotAnAddress(string value)
    {
        var context = Context(header: "CF-Connecting-IP", value: value);

        await Middleware("CF-Connecting-IP").InvokeAsync(context);

        Assert.Equal(ConnectionAddress, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task InvokeAsync_ShouldKeepTheConnectionAddress_WhenTheHeaderIsAbsent()
    {
        var context = Context();

        await Middleware("CF-Connecting-IP").InvokeAsync(context);

        Assert.Equal(ConnectionAddress, context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// Not configured means not consulted: a caller sending CF-Connecting-IP straight to an API
    /// that has no Cloudflare in front must not be able to pick their own address.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvokeAsync_ShouldIgnoreEveryHeader_WhenNoneIsConfigured(string? configured)
    {
        var context = Context(header: "CF-Connecting-IP", value: "198.51.100.7");

        await Middleware(configured).InvokeAsync(context);

        Assert.Equal(ConnectionAddress, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReadOnlyTheFirstAddress_WhenTheHeaderListsSeveral()
    {
        var context = Context(header: "X-Real-IP", value: "198.51.100.7, 203.0.113.99");

        await Middleware("X-Real-IP").InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("198.51.100.7"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public void ApplyTo_ShouldAcceptAddressesAndNetworks_AndNameWhatItCouldNotParse()
    {
        var options = new ClientIpOptions { TrustedProxies = ["10.0.0.4", "172.16.0.0/12", "", "nginx"] };
        var forwarded = new ForwardedHeadersOptions();

        var rejected = options.ApplyTo(forwarded);

        Assert.Contains(IPAddress.Parse("10.0.0.4"), forwarded.KnownProxies);
        // The framework seeds the list with loopback; ours is added beside it.
        Assert.Contains(forwarded.KnownIPNetworks, network => network.ToString() == "172.16.0.0/12");
        Assert.Equal(["nginx"], rejected);
    }

    private static ClientIpMiddleware Middleware(string? header) =>
        new(_ => Task.CompletedTask, Options.Create(new ClientIpOptions { Header = header }));

    private static DefaultHttpContext Context(string? header = null, string? value = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ConnectionAddress;
        if (header is not null)
        {
            context.Request.Headers[header] = value;
        }

        return context;
    }
}
