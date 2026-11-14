using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Dtos.KurinModule;

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
