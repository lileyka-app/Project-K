using System.Globalization;
using Microsoft.AspNetCore.Http;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.API.Middleware;

/// <summary>
/// Tells a client its access token predates a change to what the caller may do — an office, a
/// mentor assignment, a membership, the kurin's policy — so it refreshes on the next request
/// instead of when the token runs out. The request itself is served as usual: every decision is
/// made from the database anyway, the header only brings the client's menu and buttons up to date.
/// </summary>
public sealed class AccessStaleHeaderMiddleware
{
    public const string HeaderName = "X-Access-Stale";

    private readonly RequestDelegate _next;

    public AccessStaleHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAccessChangeSignal signal)
    {
        if (context.User.GetUserKey() is { } userKey && IssuedAt(context) is { } issuedAt)
        {
            var kurinKey = Guid.TryParse(context.User.FindFirst(AccessTokenClaims.KurinKey)?.Value, out var kurin)
                ? kurin
                : (Guid?)null;

            if (signal.IsStale(userKey, kurinKey, issuedAt))
            {
                // Written before the endpoint runs, so a response that starts early still carries it.
                context.Response.Headers[HeaderName] = "1";
            }
        }

        await _next(context);
    }

    // A token minted before the claim existed says nothing here; it runs out on its own.
    private static DateTimeOffset? IssuedAt(HttpContext context) =>
        long.TryParse(context.User.FindFirst(AccessTokenClaims.IssuedAt)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
