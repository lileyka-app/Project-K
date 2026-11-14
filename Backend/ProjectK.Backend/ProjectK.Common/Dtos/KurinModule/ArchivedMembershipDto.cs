using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule;

/// <param name="MembershipKey"></param>
/// <param name="MemberKey"></param>
/// <param name="MemberName"></param>
/// <param name="Kind"></param>
/// <param name="JoinedOn"></param>
/// <param name="LeftOn"></param>
/// <param name="Groups">The гуртки it passed through; known from release 1.4.</param>
/// <param name="CanCorrect"></param>
/// <param name="IsOwn"></param>
public sealed record ArchivedMembershipDto(
    Guid MembershipKey,
    Guid MemberKey,
    string MemberName,
    MembershipKind Kind,
    DateOnly JoinedOn,
    DateOnly LeftOn,
    IReadOnlyList<ArchivedStintDto> Groups,
    bool CanCorrect,
    bool IsOwn);
