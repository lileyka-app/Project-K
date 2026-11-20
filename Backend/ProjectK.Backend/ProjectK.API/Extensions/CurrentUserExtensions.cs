using Microsoft.AspNetCore.Mvc;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Extensions;

/// <summary>
/// Controller-side access to the caller's identity, so an action reads it the same way the middleware
/// and the activity logger do.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// The caller's user key, or <c>null</c> when the token carries no readable identity — an empty
    /// key included, since no account has one. Pair it with
    /// <see cref="ServiceResultExtensions.UnreadableIdentity"/>:
    /// <code>if (this.UserKey() is not { } userKey) return this.UnreadableIdentity();</code>
    /// </summary>
    public static Guid? UserKey(this ControllerBase controller)
        => controller.User.GetUserKey() is { } key && key != Guid.Empty ? key : null;
}
