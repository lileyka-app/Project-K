namespace ProjectK.Common.Records.ScoreModule;

/// <summary>
/// A youth membership as the score sees it. Only youths are scored; a впорядник is never passed in.
/// <see cref="GroupKey"/> is where they stand now — where they stood before comes from the moves.
/// </summary>
public sealed record ScoreMember(Guid MembershipKey, Guid? GroupKey, DateOnly JoinedOn, DateOnly? LeftOn);
