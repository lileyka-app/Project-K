using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Records.AuthModule;

/// <summary>
/// The account behind a member, as far as the member's own screens need it: the address it signs in
/// with and whether anyone has claimed it yet.
/// </summary>
public sealed record AccountSnapshot(Guid UserKey, string Email, OnboardingStatus Status);
