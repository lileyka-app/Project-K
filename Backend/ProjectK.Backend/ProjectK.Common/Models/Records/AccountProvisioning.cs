using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// The rules every onboarding path shares. The lifetime lives here rather than in each handler
/// because the invitation email quotes it too, and the two drifted apart once already: the letter
/// promised thirty days for a token that died after seven.
/// </summary>
public static class OnboardingPolicy
{
    /// <summary>How long an invitation stays usable after it is issued.</summary>
    public const int InvitationLifetimeDays = 7;
}

/// <summary>What a caller must know about an address before it offers an account for it.</summary>
public enum AccountAvailability
{
    /// <summary>No account and no pending waitlist entry hold this address.</summary>
    Available,

    /// <summary>An account already exists for this address.</summary>
    EmailTaken,

    /// <summary>A waitlist entry is already queued for this address; approving it is the way in.</summary>
    WaitlistPending
}

/// <summary>
/// The account to issue. <paramref name="WaitlistEntryKey"/> is the entry the invitation hangs off:
/// an approved registration names the entry it came from, while a member being given an account has
/// none — passing null opens one, already approved, on their behalf. No kurin: where the person
/// belongs is said by their membership, never by the account.
/// </summary>
public sealed record AccountProvisioningRequest(
    string Email,
    string FirstName,
    string LastName,
    Guid? WaitlistEntryKey,
    string? PhoneNumber = null,
    DateOnly? DateOfBirth = null);

/// <summary>The account that was created and the invitation that lets its owner claim it.</summary>
public sealed record AccountProvisioningResult(Guid UserKey, Guid InvitationKey, string InvitationToken);

/// <summary>
/// The account behind a member, as far as the member's own screens need it: the address it signs in
/// with and whether anyone has claimed it yet.
/// </summary>
public sealed record AccountSnapshot(Guid UserKey, string Email, OnboardingStatus Status);
