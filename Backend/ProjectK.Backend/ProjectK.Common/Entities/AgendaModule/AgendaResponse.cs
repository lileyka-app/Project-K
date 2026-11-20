using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Entities.AgendaModule;

/// <summary>
/// One member's RSVP to one occurrence of an event. Uniqueness is (item, user, occurrence): a fresh
/// answer overwrites the previous one for that occurrence only. Confirmed-vs-waitlist is derived at
/// read time from the category capacity and <see cref="RespondedAtUtc"/>, not stored, so a capacity
/// change re-ranks everyone without a migration.
/// </summary>
public class AgendaResponse : Entity
{
    public Guid AgendaResponseKey { get; set; } = Guid.NewGuid();
    public Guid AgendaItemKey { get; set; }
    public Guid UserKey { get; set; }

    /// <summary>
    /// Which occurrence the answer is for: null for a one-off event, the occurrence start for a series.
    /// Readers filter on it exactly, so a row of the other shape (left behind when an item switches
    /// between one-off and recurring) is simply not seen; there is no «whole series» fallback.
    /// </summary>
    public DateTime? OccurrenceStartUtc { get; set; }

    public AgendaRsvpStatus Status { get; set; }
    public DateTime RespondedAtUtc { get; set; } = DateTime.UtcNow;

    public AgendaItem AgendaItem { get; set; } = null!;
}
