using System.Net;
using Microsoft.Extensions.Options;
using ProjectK.API.Helpers;

namespace ProjectK.API.Middleware;

/// <summary>
/// Makes the configured client-address header the connection's remote address, so every consumer
/// downstream — the rate limiter partitions, the geo-block, the IP-change log — reads one value
/// without knowing which proxy stands in front. Runs right after the forwarded-headers middleware
/// and only when <see cref="ClientIpOptions.Header"/> is set; with the header absent or unparseable
/// the request keeps the address it arrived with.
/// </summary>
public sealed class ClientIpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _header;

    public ClientIpMiddleware(RequestDelegate next, IOptions<ClientIpOptions> options)
    {
        _next = next;
        _header = string.IsNullOrWhiteSpace(options.Value.Header) ? null : options.Value.Header.Trim();
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_header is not null
            && context.Request.Headers.TryGetValue(_header, out var values)
            && IPAddress.TryParse(values.ToString().Split(',')[0].Trim(), out var address))
        {
            context.Connection.RemoteIpAddress = address;
        }

        return _next(context);
    }
}
