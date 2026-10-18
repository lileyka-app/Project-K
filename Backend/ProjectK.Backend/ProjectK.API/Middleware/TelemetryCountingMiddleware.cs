using Microsoft.AspNetCore.Http;
using ProjectK.Infrastructure.Services.Telemetry;

namespace ProjectK.API.Middleware;

/// <summary>
/// Counts API requests and 5xx answers for the daily telemetry ping. <c>/health</c> is left out:
/// the container probe calls it every 15 seconds and would drown the real traffic.
/// </summary>
public sealed class TelemetryCountingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryCounters _counters;

    public TelemetryCountingMiddleware(RequestDelegate next, TelemetryCounters counters)
    {
        _next = next;
        _counters = counters;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch
        {
            _counters.Count(StatusCodes.Status500InternalServerError);
            throw;
        }

        _counters.Count(context.Response.StatusCode);
    }
}
