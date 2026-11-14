using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Extensions;

public static class AppUserSignInExtensions
{
    /// <summary>
    /// Whether the account may open a session at all. Suspended and archived accounts are refused
    /// at sign-in, at the second factor and at refresh alike — before any of this, those two
    /// statuses were written nowhere and read nowhere, so an administrator's only way to stop an
    /// account was to delete it. Accounts that never finished onboarding (<c>RegisteredInactive</c>,
    /// <c>PendingActivation</c>) are not refused here: the first has existed since before onboarding
    /// statuses did, and the second holds no password until activation makes it Active.
    /// </summary>
    public static bool CanSignIn(this AppUser user)
        => user.OnboardingStatus is not (OnboardingStatus.Suspended or OnboardingStatus.Archived);
}
