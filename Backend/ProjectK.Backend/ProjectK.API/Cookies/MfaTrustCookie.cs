using Microsoft.AspNetCore.Http;

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
        var isSecureRequest = IsSecureRequest(context.Request);
        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecureRequest,
            SameSite = isSecureRequest ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = expiresUtc,
            Path = "/api/auth"
        });
    }

    public static string? Read(HttpRequest request)
    {
        var value = request.Cookies[Name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsSecureRequest(HttpRequest request)
    {
        return request.IsHttps
            || string.Equals(request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase);
    }
}
