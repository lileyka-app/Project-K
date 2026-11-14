using Microsoft.AspNetCore.Http;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Cookies;

/// <summary>
/// The sessions this browser keeps for the accounts it is signed in to but not using. Scoped like
/// the refresh cookie and just as unreadable to scripts: each value in it is a refresh token.
/// <para>
/// Always <c>Secure</c>, unlike the refresh cookie: it holds other accounts' sessions, so it never
/// travels over plain HTTP. Browsers treat <c>localhost</c> as secure, so local development keeps
/// working; a self-hosted instance served over plain HTTP elsewhere simply keeps no other accounts.
/// </para>
/// </summary>
public static class SavedSessionsCookie
{
    public const string Name = "savedSessions";
    private const char Separator = ',';
    private const string Path = "/api/auth";

    public static void Write(HttpContext context, IReadOnlyCollection<SavedSession> sessions)
    {
        if (sessions.Count == 0)
        {
            Delete(context);
            return;
        }

        context.Response.Cookies.Append(Name, string.Join(Separator, sessions.Select(session => session.Token)), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteFor(context.Request),
            Expires = sessions.Max(session => session.ExpiresAtUtc),
            Path = Path
        });
    }

    public static void Delete(HttpContext context)
    {
        context.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteFor(context.Request),
            Path = Path
        });
    }

    /// <summary>The kept tokens, oldest first.</summary>
    public static List<string> Read(HttpRequest request)
    {
        return request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];
    }

    /// <summary>
    /// Cross-site over HTTPS, where the web and the API sit on different hosts; same-site over
    /// local HTTP, where a browser refuses <c>None</c> without a secure origin.
    /// </summary>
    private static SameSiteMode SameSiteFor(HttpRequest request)
    {
        var isSecureRequest = request.IsHttps
            || string.Equals(request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase);
        return isSecureRequest ? SameSiteMode.None : SameSiteMode.Lax;
    }
}
