using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>A пересторога as the report reads it, revoked and expired ones included.</summary>
public sealed record KurinReportSourceWarning(
    MemberWarningLevel Level,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc);
