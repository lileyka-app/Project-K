using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>The third probe's file as the person writes it, with the stages and what its files take.</summary>
/// <param name="OpenedOn">The day the probe opened, or else the day the previous probe was closed.</param>
/// <param name="OpenedOnIsDerived">True when <paramref name="OpenedOn"/> was taken from the previous probe.</param>
/// <param name="StoredBytes">What the person's pictures and посвідка take, counted against the cap.</param>
public sealed record ThirdProbeFileDto(
    DateOnly? OpenedOn,
    bool OpenedOnIsDerived,
    string? MentorZvyazkovyi,
    string? MentorVporiadnyk,
    string? MentorReferent,
    DateOnly? ReflectionOn,
    string? ReflectionWith,
    DateTime? ReflectionConfirmedAtUtc,
    string? ReflectionConfirmedByName,
    ThirdProbeCampKind? CampKind,
    string? CampName,
    DateOnly? CampFrom,
    DateOnly? CampTo,
    ThirdProbeCertificateDto? CampCertificate,
    string? PhysicalBadgeId,
    string Resume,
    ThirdProbeStagesDto Stages,
    long StoredBytes,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);
