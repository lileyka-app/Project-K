using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>
/// The cover of the box: who the person is, and nothing that needs permission of its own. The
/// private details of a profile — address, school — stay with <c>GET api/member/{key}</c>, which
/// already decides who may see them; repeating that decision here would mean repeating a security
/// rule in two places. The two it does carry follow the same rules: the date of birth goes through
/// the kurin's mask, and the public code is told only to its owner.
/// </summary>
public sealed record MemberDossierProfileDto(
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
