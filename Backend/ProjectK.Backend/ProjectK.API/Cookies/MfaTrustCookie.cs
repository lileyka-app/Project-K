namespace ProjectK.API.Cookies;

/// <summary>
/// The cookie that remembers a device finished the second factor. Scoped like the refresh-token
/// cookie, but with its own lifetime and never deleted on sign-out: the point is that signing
/// out and back in on the same device does not ask for the code again.
/// </summary>
public static class MfaTrustCookie
{
    public const string Name = "mfaTrust";

    public static void Set(HttpContext context, string token, DateTime expiresUtc)
    {
        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = AuthCookie.IsSecure(context.Request),
            SameSite = AuthCookie.SameSiteFor(context.Request),
            Expires = expiresUtc,
            Path = AuthCookie.Path
        });
    }

    public static string? Read(HttpRequest request)
    {
        var value = request.Cookies[Name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
