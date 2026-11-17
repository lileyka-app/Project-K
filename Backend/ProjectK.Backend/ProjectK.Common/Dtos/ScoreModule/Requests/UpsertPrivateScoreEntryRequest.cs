namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>Points the КВ gives a youth among themselves: for a criterion of theirs or just with a note.</summary>
public sealed class UpsertPrivateScoreEntryRequest
{
    public Guid MembershipKey { get; set; }
    public Guid? PrivateScoreCriterionKey { get; set; }
    public int Points { get; set; }
    public string? Note { get; set; }
    public DateOnly OccurredOn { get; set; }
}
