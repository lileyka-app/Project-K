namespace ProjectK.Common.Records.AuthModule;

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
