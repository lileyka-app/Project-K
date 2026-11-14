using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A провід — a kurin's, its КВ or a гурток's — with everyone who has held its roles.</summary>
/// <param name="EntityKey">The kurin or the гурток it leads, whichever <paramref name="Type"/> says.</param>
public sealed record LeadershipDto(
    Guid LeadershipKey,
    LeadershipType Type,
    Guid EntityKey,
    Guid? KurinKey,
    Guid? GroupKey,
    IEnumerable<LeadershipHistoryMemberDto> LeadershipHistories,
    DateOnly StartDate,
    DateOnly? EndDate);
