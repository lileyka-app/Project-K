using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportProbe(
    string ProbeId,
    string ProbeTitle,
    ProbeProgressStatus Status,
    string StatusLabel,
    DateTime? CompletedAtUtc,
    string? CompletedByName,
    DateTime? VerifiedAtUtc,
    string? VerifiedByName);
