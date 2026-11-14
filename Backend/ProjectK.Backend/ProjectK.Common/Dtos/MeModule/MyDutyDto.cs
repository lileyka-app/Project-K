using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>One thing waiting on the person in one kurin, with enough to go straight to it.</summary>
public sealed record MyDutyDto
{
    public required MyDutyKind Kind { get; init; }
    public required MyKurinRefDto Kurin { get; init; }
    /// <summary>How many of them; one for an event.</summary>
    public required int Count { get; init; }
    /// <summary>The гурток whose box it is; null for the kurin's own box or the whole kurin.</summary>
    public Guid? GroupKey { get; init; }
    public string? GroupName { get; init; }
    /// <summary>For an event: which one and when.</summary>
    public Guid? AgendaItemKey { get; init; }
    public DateTime? OccurrenceStartUtc { get; init; }
    public string? Title { get; init; }
    /// <summary>Which duty this is, the same on every read: what hiding it names.</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>How big it is now; hidden, it comes back when this grows.</summary>
    public string Fingerprint { get; init; } = string.Empty;
}
