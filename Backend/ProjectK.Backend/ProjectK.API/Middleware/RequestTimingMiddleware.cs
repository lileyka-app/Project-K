using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace ProjectK.API.Middleware;

/// <summary>
/// Feeds <see cref="RequestTimings"/>. Leaves out what is not work: the probes themselves
/// (<c>/health</c>, the root) and CORS preflights, which answer in microseconds and would pull the
/// median down to nothing on a quiet day.
/// </summary>
public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestTimings _timings;

    public RequestTimingMiddleware(RequestDelegate next, RequestTimings timings)
    {
        _next = next;
        _timings = timings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!Counts(context.Request))
        {
            await _next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            _timings.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    public static bool Counts(HttpRequest request) =>
        !HttpMethods.IsOptions(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && request.Path != "/"
        && !request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
