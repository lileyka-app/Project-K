using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// A person's archive box. The index says which folders there are and how full each one is; a folder
/// is filled in only if it was asked for, and is null otherwise — so a caller reading one thing does
/// not pay for the rest, and a caller reading everything does it in one request.
/// </summary>
public sealed record MemberDossierDto(
    MemberDossierProfileDto Profile,
    IReadOnlyCollection<MemberDossierFolder> Folders,
    IReadOnlyCollection<MembershipRecord>? Memberships,
    IReadOnlyCollection<LeadershipHistoryDto>? Offices,
    IReadOnlyCollection<PlastLevelHistoryDto>? Levels,
    IReadOnlyCollection<MemberAwardDto>? Awards,
    IReadOnlyCollection<MemberWarningDto>? Warnings,
    MemberProgress? Progress);
