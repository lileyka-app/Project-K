using ProjectK.Common.Dtos.IdentityModule;

namespace ProjectK.API.Cookies;

/// <summary>
/// The refresh-token cookie, written and read in one place. Sign-in, refresh, sign-out and the
/// dev role switcher all hand out or end a session, and each used to carry its own copy of how
/// the cookie is scoped.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "refreshToken";

    /// <summary>Hands out the session a sign-in, refresh or switch just issued.</summary>
    public static void Set(HttpContext context, RefreshToken token) => Set(context, token.Token, token.Expires);

    public static void Set(HttpContext context, string token, DateTime expires)
    {
        Delete(context);

        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = AuthCookie.IsSecure(context.Request),
            SameSite = AuthCookie.SameSiteFor(context.Request),
            Expires = expires,
            Path = AuthCookie.Path
        });
    }

    /// <summary>Deletes under every path the cookie has ever been written with, so an old copy cannot linger.</summary>
    public static void Delete(HttpContext context)
    {
        var isSecureRequest = AuthCookie.IsSecure(context.Request);
        foreach (var path in new[] { AuthCookie.Path, "/api", "/" })
        {
            context.Response.Cookies.Delete(Name, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecureRequest,
                SameSite = AuthCookie.SameSiteFor(context.Request),
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
}
