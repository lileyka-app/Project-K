using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportWarning(
    MemberWarningLevel Level,
    string LevelLabel,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsRevoked);
