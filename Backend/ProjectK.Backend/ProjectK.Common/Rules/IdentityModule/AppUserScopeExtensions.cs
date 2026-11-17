using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Rules.IdentityModule;

public static class AppUserScopeExtensions
{
    /// <summary>
    /// The kurin the account has stepped into, or null when it never has. Every place that mints
    /// an access token must go through this, or a refresh would silently widen an admin back to
    /// system-wide access. The account used to carry a kurin of its own as a fallback; belonging
    /// is read from membership now (<c>AccessContextResolver</c>), so there is nothing to fall back to.
    /// </summary>
    public static Guid? ResolveScopeKurinKey(this AppUser user)
    {
        return Normalize(user.ActiveKurinKey);
    }

    public static string? ResolveScopeKurinKeyString(this AppUser user)
    {
        return user.ResolveScopeKurinKey()?.ToString();
    }

    private static Guid? Normalize(Guid? kurinKey)
    {
        return kurinKey is null || kurinKey == Guid.Empty ? null : kurinKey;
    }
}
