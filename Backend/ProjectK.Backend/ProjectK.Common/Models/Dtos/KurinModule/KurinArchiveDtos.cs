using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>The kurin's archive, as far as the viewer reaches it.</summary>
/// <param name="Offices">Every holding of an office, current ones too, newest first.</param>
/// <param name="Memberships">The memberships that ended.</param>
/// <param name="DissolvedGroups"></param>
/// <param name="CanDelete">Whether the viewer may turn the danger zone on.</param>
public sealed record KurinArchiveDto(
    IReadOnlyList<ArchivedOfficeDto> Offices,
    IReadOnlyList<ArchivedMembershipDto> Memberships,
    IReadOnlyList<DissolvedGroupDto> DissolvedGroups,
    bool CanDelete);

/// <param name="HistoryKey"></param>
/// <param name="Type"></param>
/// <param name="Role"></param>
/// <param name="MemberKey"></param>
/// <param name="MemberName"></param>
/// <param name="StartDate"></param>
/// <param name="EndDate">Null while it is held; a current holding is changed on the провід, not here.</param>
/// <param name="GroupKey"></param>
/// <param name="GroupName"></param>
/// <param name="GroupDissolved"></param>
/// <param name="CanCorrect">Whether the viewer may correct it: a former holding, within reach, not their own.</param>
/// <param name="IsOwn">The viewer's own holding: never corrected nor deleted by them.</param>
public sealed record ArchivedOfficeDto(
    Guid HistoryKey,
    LeadershipType Type,
    LeadershipRole Role,
    Guid MemberKey,
    string MemberName,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? GroupKey,
    string? GroupName,
    bool GroupDissolved,
    bool CanCorrect,
    bool IsOwn);

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

/// <param name="GroupKey"></param>
/// <param name="GroupName">Null once the гурток is deleted for good.</param>
/// <param name="From">Null for a stretch already running when the record began.</param>
/// <param name="To"></param>
public sealed record ArchivedStintDto(Guid GroupKey, string? GroupName, DateOnly? From, DateOnly? To);

public sealed record DissolvedGroupDto(Guid GroupKey, string Name, DateOnly DissolvedOn);

/// <summary>The danger zone as the server holds it.</summary>
/// <param name="Ticket">Sent back in <c>X-Archive-Danger</c> with every deletion; kept in the page's memory only.</param>
/// <param name="ExpiresAtUtc"></param>
/// <param name="DeletionsLeft"></param>
public sealed record ArchiveDangerZoneDto(string Ticket, DateTime ExpiresAtUtc, int DeletionsLeft);

/// <param name="DeletionsLeft">What the ticket still allows; at zero the zone has ended.</param>
public sealed record ArchiveDeletionDto(int DeletionsLeft);
