using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Records.IdentityModule;

/// <summary>One thing a duty source found waiting on the viewer in their kurin; the collector names the kurin and the key.</summary>
/// <param name="Count">How many of them; one for an event.</param>
/// <param name="GroupKey">The гурток whose box it is; null for the kurin's own box or the whole kurin.</param>
/// <param name="AgendaItemKey">For an event: which one; <paramref name="OccurrenceStartUtc"/> says when.</param>
public sealed record PendingDuty(
    MyDutyKind Kind,
    int Count,
    Guid? GroupKey = null,
    string? GroupName = null,
    Guid? AgendaItemKey = null,
    DateTime? OccurrenceStartUtc = null,
    string? Title = null);
