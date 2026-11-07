using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// One holding of an office in the kurin, current or past, with the гурток it was in. A dissolved
/// гурток still names its holdings: that is what the archive keeps it for.
/// </summary>
public sealed record ArchivedOffice(
    Guid HistoryKey,
    Guid LeadershipKey,
    LeadershipType Type,
    LeadershipRole Role,
    Guid MemberKey,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? GroupKey,
    string? GroupName,
    bool GroupDissolved);

/// <summary>A membership that has ended, with the гуртки it passed through.</summary>
public sealed record ArchivedMembership(
    Guid MembershipKey,
    Guid MemberKey,
    Guid? UserKey,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime LeftAtUtc,
    IReadOnlyList<ArchivedStint> Stints);

/// <summary>A stretch in one гурток. A null start is one already running when the record began.</summary>
public sealed record ArchivedStint(Guid GroupKey, string? GroupName, DateTime? FromUtc, DateTime? ToUtc);

public sealed record DissolvedGroup(Guid GroupKey, string Name, DateTime DissolvedAtUtc);
