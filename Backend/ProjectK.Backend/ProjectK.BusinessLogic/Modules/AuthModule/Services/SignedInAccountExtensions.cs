using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The lookup behind every "my account" action: the caller acts on the account its token names.
/// A miss is answered with <see cref="AuthErrors.AccountMissing{T}"/>, as an expired sign-in.
/// </summary>
public static class SignedInAccountExtensions
{
    /// <summary>
    /// The account the signed-in caller holds a token for, or <c>null</c> when it is gone. Callers
    /// answer a <c>null</c> with <see cref="AuthErrors.AccountMissing{T}"/>.
    /// </summary>
    public static Task<AppUser?> FindSignedInAsync(this UserManager<AppUser> userManager, Guid accountKey)
        => userManager.FindByIdAsync(accountKey.ToString());
}
