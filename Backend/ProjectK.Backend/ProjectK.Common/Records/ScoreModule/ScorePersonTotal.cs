using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Records.ScoreModule;

/// <summary>One person's points in a period — in one гурток, or in all of them.</summary>
public sealed record ScorePersonTotal(Guid MembershipKey, int Total, IReadOnlyDictionary<ScoreSource, int> BySource);
