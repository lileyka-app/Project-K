namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// The person a public code names, as a провід confirms them before asking for them: enough to be
/// sure it is the right person, and how many kurins they stand in now — never which. Which ones this
/// kurin may know is <see cref="MembershipCandidateDto.CurrentKurins"/>'s to say.
/// </summary>
public sealed record MembershipCandidateMemberDto(
    Guid MemberKey,
    string PublicId,
    string FirstName,
    string LastName,
    string? ProfilePhotoBlobName,
    int CurrentMembershipCount,
    string FullName);
