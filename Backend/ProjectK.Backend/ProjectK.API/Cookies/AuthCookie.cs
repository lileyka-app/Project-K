using Microsoft.AspNetCore.Http;

namespace ProjectK.API.Cookies;

/// <summary>
/// What every sign-in cookie shares: the path it is scoped to and how the request decides its
/// transport. Each cookie used to carry its own copy of the HTTPS check.
/// </summary>
internal static class AuthCookie
{
    public const string Path = "/api/auth";

    /// <summary>HTTPS directly, or behind a proxy that says so.</summary>
    public static bool IsSecure(HttpRequest request)
    {
        return request.IsHttps
            || string.Equals(request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cross-site over HTTPS, where the web and the API sit on different hosts; same-site over
    /// local HTTP, where a browser refuses <c>None</c> without a secure origin.
    /// </summary>
    public static SameSiteMode SameSiteFor(HttpRequest request)
    {
        return IsSecure(request) ? SameSiteMode.None : SameSiteMode.Lax;
    }
}
