using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <param name="Description">Markdown, like the goal, plan and results; pictures are
/// <c>project-image:{key}</c>, their links in <paramref name="Images"/>.</param>
public sealed record ProbeProjectResponse(
    Guid ProjectKey,
    string Title,
    ProbeProjectArea? Area,
    IReadOnlyList<ProbeProjectReach> Reach,
    bool IsShared,
    string? SharedWith,
    bool IsSpare,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Location,
    string? EventLeads,
    string? CoordinatorName,
    string? CoordinatorContact,
    string Description,
    string Goal,
    string Plan,
    string Results,
    DateTime? ApprovedAtUtc,
    string? ApprovedByName,
    DateTime? CompletedAtUtc,
    string? CompletedByName,
    string? CoordinatorComment,
    DateTime UpdatedAtUtc,
    string? UpdatedByName,
    IReadOnlyList<ProbeProjectImageResponse> Images);

/// <param name="Url">A signed link that expires; never store it, store the key.</param>
public sealed record ProbeProjectImageResponse(Guid ImageKey, string Url);

/// <param name="Url">A signed link that expires.</param>
public sealed record ThirdProbeCertificateResponse(string Url, string ContentType, string? FileName);

/// <summary>
/// The stages the referent of the third probe tracks, each the day it happened; null until then.
/// The same shape travels to the referent's own tool (<see cref="ThirdProberStatusResponse"/>).
/// </summary>
public sealed record ThirdProbeStagesResponse(
    DateOnly? MotivationLetterOn,
    DateOnly? SelfReflectionOn,
    DateOnly? ApplicationSubmittedOn,
    DateOnly? OpenedOn,
    DateOnly? CompletedOn,
    DateOnly? NamedOn,
    DateOnly? ArchivedOn,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <param name="OpenedOn">The day the probe opened, or else the day the previous probe was closed.</param>
/// <param name="OpenedOnIsDerived">True when <paramref name="OpenedOn"/> was taken from the previous probe.</param>
/// <param name="StoredBytes">What the person's pictures and посвідка take, counted against the cap.</param>
public sealed record ThirdProbeFileResponse(
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
    ThirdProbeCertificateResponse? CampCertificate,
    string? PhysicalBadgeId,
    string Resume,
    ThirdProbeStagesResponse Stages,
    long StoredBytes,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>A вмілість the person holds, confirmed.</summary>
/// <param name="DuringProbe">Confirmed on or after the day the third probe opened.</param>
public sealed record ThirdProbeBadgeResponse(
    string BadgeId,
    string Title,
    string? ImagePath,
    string? Specialization,
    DateTime? ConfirmedAtUtc,
    bool DuringProbe);

/// <summary>The limits as numbers, so a client says the same thing the server enforces.</summary>
public sealed record ProbeProjectLimitsResponse(
    int MaxMainProjects,
    int MaxSpareProjects,
    int MaxSharedMainProjects,
    int MaxImagesPerProject,
    long MaxImageBytes,
    long MaxCertificateBytes,
    long MaxStoredBytes,
    int MaxTextLength,
    int RequiredBadges,
    int YouthEndsAtAge);

/// <summary>Everything the «Третя проба» tab shows.</summary>
/// <param name="IsProjectProbe">Whether the probe stands at the third place of the person's line.
/// Only that one has a file and projects; for any other the rest is empty.</param>
/// <param name="CanEdit">The person themself, or whoever the kurin lets sign their probe.</param>
/// <param name="CanSign">May confirm the reflection, a project's start and its completion, and set the stages.</param>
/// <param name="CanUnsign">May take such a confirmation back.</param>
public sealed record ThirdProbeResponse(
    bool IsProjectProbe,
    bool CanEdit,
    bool CanSign,
    bool CanUnsign,
    ThirdProbeFileResponse? File,
    IReadOnlyList<ProbeProjectResponse> Projects,
    IReadOnlyList<ThirdProbeBadgeResponse> Badges,
    ProbeProjectLimitsResponse Limits);

/// <summary>
/// One third-prober as the referent's tool in Lilejka will read them (`GET api/kurin/{kurinKey}/third-probers`):
/// who, which probe, the stages, and how far the requirements are. Fields are added, never renamed,
/// so that tool can be built on top of this list.
/// </summary>
public sealed record ThirdProberStatusResponse(
    Guid MemberKey,
    string FirstName,
    string LastName,
    string ProbeId,
    ThirdProbeStagesResponse Stages,
    int MainProjects,
    int CompletedProjects,
    int BadgesDuringProbe,
    bool HasCamp,
    bool HasResume,
    bool ReflectionConfirmed);
