using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// The cover of the box: who the person is, and nothing that needs permission of its own. The
/// private details of a profile — address, school — stay with <c>GET api/member/{key}</c>, which
/// already decides who may see them; repeating that decision here would mean repeating a security
/// rule in two places. The two it does carry follow the same rules: the date of birth goes through
/// the kurin's mask, and the public code is told only to its owner.
/// </summary>
public sealed record MemberDossierProfile(
    Guid MemberKey,
    string? PublicId,
    Guid? UserKey,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly? DateOfBirth,
    string? ProfilePhotoUrl,
    PlastLevel? LatestPlastLevel,
    MemberProfileVerificationStatus VerificationStatus,
    DateTime? VerifiedAtUtc);

/// <summary>
/// A person's archive box. The index says which folders there are and how full each one is; a folder
/// is filled in only if it was asked for, and is null otherwise — so a caller reading one thing does
/// not pay for the rest, and a caller reading everything does it in one request.
/// </summary>
public sealed class MemberDossierResponse
{
    public required MemberDossierProfile Profile { get; init; }

    public IReadOnlyCollection<MemberDossierFolder> Folders { get; init; } = [];

    public IReadOnlyCollection<MembershipRecord>? Memberships { get; init; }
    public IReadOnlyCollection<LeadershipHistoryDto>? Offices { get; init; }
    public IReadOnlyCollection<PlastLevelHistoryDto>? Levels { get; init; }
    public IReadOnlyCollection<MemberAwardDto>? Awards { get; init; }
    public IReadOnlyCollection<MemberWarningDto>? Warnings { get; init; }
    public MemberProgress? Progress { get; init; }
}
