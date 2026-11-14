using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>One probe, as far as a person has taken it.</summary>
public sealed record ProbeProgressRecord(
    string ProbeId,
    ProbeProgressStatus Status,
    Guid KurinKey,
    DateTime? CompletedAtUtc,
    DateTime? VerifiedAtUtc);
