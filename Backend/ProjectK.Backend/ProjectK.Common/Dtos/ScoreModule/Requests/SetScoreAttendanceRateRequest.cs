namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>
/// What being at an event is worth, for a group of events or for one event; exactly one key is set.
/// Null points take an event's own rate off, so it is worth its group's again.
/// </summary>
public sealed class SetScoreAttendanceRateRequest
{
    public Guid? AgendaCategoryKey { get; set; }
    public Guid? AgendaItemKey { get; set; }
    public int? Points { get; set; }
}
