using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Records.ScoreModule;

/// <summary>Points earned once: who, for which гурток (if any), from where, when.</summary>
public sealed record ScoreLine(Guid MembershipKey, Guid? GroupKey, ScoreSource Source, int Points, DateOnly On);
