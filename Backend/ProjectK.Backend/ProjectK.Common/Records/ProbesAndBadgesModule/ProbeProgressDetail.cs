using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>One проба with the names the звіт prints: who closed it and who verified it.</summary>
public sealed record ProbeProgressDetail(
    string ProbeId,
    ProbeProgressStatus Status,
    DateTime? CompletedAtUtc,
    string? CompletedByName,
    DateTime? VerifiedAtUtc,
    string? VerifiedByName);
