using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>One вмілість, as far as a person has taken it.</summary>
public sealed record BadgeProgressRecord(
    string BadgeId,
    BadgeProgressStatus Status,
    Guid KurinKey,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc);
