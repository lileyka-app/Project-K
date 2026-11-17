using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Records.IdentityModule;

/// <summary>One thing a duty source found waiting on the viewer in their kurin; the collector names the kurin and the key.</summary>
/// <param name="Kind">What it is about; the dashboard knows where each kind is done.</param>
/// <param name="Count">How many of them; one for an event.</param>
/// <param name="GroupKey">The гурток whose box it is; null for the kurin's own box or the whole kurin.</param>
/// <param name="GroupName">That гурток's name.</param>
/// <param name="AgendaItemKey">For an event: which one.</param>
/// <param name="OccurrenceStartUtc">For an event: which occurrence.</param>
/// <param name="Title">For an event: its title.</param>
public sealed record PendingDuty(
    MyDutyKind Kind,
    int Count,
    Guid? GroupKey = null,
    string? GroupName = null,
    Guid? AgendaItemKey = null,
    DateTime? OccurrenceStartUtc = null,
    string? Title = null);
