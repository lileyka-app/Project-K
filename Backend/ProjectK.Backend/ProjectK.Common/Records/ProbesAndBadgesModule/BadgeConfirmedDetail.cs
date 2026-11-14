using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>One confirmed вмілість, with who confirmed it and in what capacity.</summary>
public sealed record BadgeConfirmedDetail(
    string BadgeId,
    BadgeProgressStatus Status,
    DateTime? ReviewedAtUtc,
    string? ReviewedByName,
    string? ReviewedByRole);
