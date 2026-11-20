using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportBadge(
    string BadgeId,
    string BadgeTitle,
    BadgeProgressStatus Status,
    string StatusLabel,
    DateTime? ReviewedAtUtc,
    string? ReviewedByName,
    string? ReviewedByRole);
