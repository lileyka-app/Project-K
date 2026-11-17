using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Rules.IdentityModule;

/// <summary>
/// The lookup behind every "my account" action: the caller acts on the account its token names.
/// A miss is answered as an expired sign-in, with the module's <c>AccountMissing</c>.
/// </summary>
public static class SignedInAccountExtensions
{
    /// <summary>
    /// The account the signed-in caller holds a token for, or <c>null</c> when it is gone. Callers
    /// answer a <c>null</c> as an expired sign-in.
    /// </summary>
    public static Task<AppUser?> FindSignedInAsync(this UserManager<AppUser> userManager, Guid accountKey)
        => userManager.FindByIdAsync(accountKey.ToString());
}
