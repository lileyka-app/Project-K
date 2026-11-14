using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportWarning(
    MemberWarningLevel Level,
    string LevelLabel,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsRevoked);
