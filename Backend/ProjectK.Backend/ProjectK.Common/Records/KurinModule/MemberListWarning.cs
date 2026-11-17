using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>A пересторога of a person on a roster, as stored; the roster reads only those not revoked.</summary>
public sealed record MemberListWarning(
    Guid MemberWarningKey,
    Guid MemberKey,
    MemberWarningLevel Level,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    Guid IssuedByUserKey,
    Guid? RevokedByUserKey,
    DateTime? RevokedAtUtc);
