using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Records.KurinModule;

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
