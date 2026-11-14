using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// A провід — a kurin's, its КВ or a гурток's — with everyone who has held its roles.
/// <c>EntityKey</c> is the kurin or the гурток it leads, whichever <c>Type</c> says.
/// </summary>
public sealed record LeadershipDto(
    Guid LeadershipKey,
    LeadershipType Type,
    Guid EntityKey,
    Guid? KurinKey,
    Guid? GroupKey,
    IEnumerable<LeadershipHistoryMemberDto> LeadershipHistories,
    DateOnly StartDate,
    DateOnly? EndDate);
