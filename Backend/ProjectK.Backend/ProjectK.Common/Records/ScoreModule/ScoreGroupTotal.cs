namespace ProjectK.Common.Records.ScoreModule;

/// <summary>One гурток's standing in a period.</summary>
/// <param name="YouthPoints">Everything its youths earned while in it.</param>
/// <param name="YouthCount">How many youths it had, a youth there for half the period counting a half.</param>
/// <param name="Average">The youths' points per youth.</param>
/// <param name="GroupPoints">What was given to the гурток as a whole; never divided.</param>
/// <param name="Score">What it is ranked by: the average or the sum, as the kurin chose, plus <paramref name="GroupPoints"/>.</param>
/// <param name="OtherScore">The same by the other method, shown alongside.</param>
public sealed record ScoreGroupTotal(
    Guid GroupKey,
    int YouthPoints,
    decimal YouthCount,
    decimal Average,
    int GroupPoints,
    decimal Score,
    decimal OtherScore);
