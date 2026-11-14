namespace ProjectK.API.Helpers;

/// <summary>
/// The refresh-token cookie, written and read in one place. Sign-in, refresh, sign-out and the
/// dev role switcher all hand out or end a session, and each used to carry its own copy of how
/// the cookie is scoped.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "refreshToken";

    public static void Set(HttpContext context, string token, DateTime expires)
    {
        Delete(context);

        var isSecureRequest = IsSecureRequest(context.Request);
        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecureRequest,
            SameSite = isSecureRequest ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = expires,
            Path = "/api/auth"
        });
    }

    /// <summary>Deletes under every path the cookie has ever been written with, so an old copy cannot linger.</summary>
    public static void Delete(HttpContext context)
    {
        var isSecureRequest = IsSecureRequest(context.Request);
        foreach (var path in new[] { "/api/auth", "/api", "/" })
        {
            context.Response.Cookies.Delete(Name, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecureRequest,
                SameSite = isSecureRequest ? SameSiteMode.None : SameSiteMode.Lax,
                Path = path
            });
        }
    }

    /// <summary>
    /// Every value the request carries under the cookie name. Read from the raw header rather than
    /// the parsed cookie collection, which keeps only one value per name: a browser that holds two
    /// copies from different paths sends both, and sign-out has to end both.
    /// </summary>
    public static List<string> Read(HttpRequest request)
    {
        return request.Headers.Cookie
            .SelectMany(header => header?.Split(';') ?? [])
            .Select(cookie => cookie.Trim())
            .Where(cookie => cookie.StartsWith($"{Name}=", StringComparison.Ordinal))
            .Select(cookie => cookie[(Name.Length + 1)..])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Uri.UnescapeDataString(value.Trim('"')))
            .ToList();
    }

    private static bool IsSecureRequest(HttpRequest request)
    {
        return request.IsHttps
            || string.Equals(request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase);
    }
}
