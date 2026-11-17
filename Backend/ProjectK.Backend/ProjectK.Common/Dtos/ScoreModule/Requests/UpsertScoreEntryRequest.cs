namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>
/// Points by hand: to a person or to a гурток (one of the two), as a position from the list or as a
/// free amount with a reason, at an event (then with its occurrence) or not.
/// </summary>
public sealed class UpsertScoreEntryRequest
{
    public Guid? MembershipKey { get; set; }
    public Guid? GroupKey { get; set; }
    public Guid? ScoreItemKey { get; set; }
    public int? Points { get; set; }
    public string? Reason { get; set; }
    public Guid? AgendaItemKey { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateOnly OccurredOn { get; set; }
}
