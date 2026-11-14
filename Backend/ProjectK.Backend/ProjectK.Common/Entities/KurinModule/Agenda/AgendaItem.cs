using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule.Agenda;

/// <summary>
/// A calendar event or a board task inside a kurin. Dates are stored as full UTC even though the UI
/// renders them per-day, so switching to an hourly view later needs no migration. Who sees an item
/// is decided by its <see cref="Assignments"/>, not by a single owner column.
/// </summary>
public class AgendaItem : Entity
{
    public Guid AgendaItemKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public AgendaItemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Where it happens, as people say it: «домівка, мала кімната», «Голосіївський парк».</summary>
    public string? Location { get; set; }

    /// <summary>
    /// Board column. Meaningful for <see cref="AgendaItemKind.Task"/>; events stay Todo. Once the item
    /// has targets it is derived from their states on every write (all done → done, any started → in
    /// progress) and kept stored so feeds can filter on it.
    /// </summary>
    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.Todo;

    /// <summary>Full UTC start; null means the item is not yet placed on the calendar.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>Full UTC end; null for a single-day item.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>True while the calendar runs in per-day mode; kept so an hourly mode can honour it.</summary>
    public bool IsAllDay { get; set; } = true;

    /// <summary>The event group (табір/захід/сходини) this item belongs to; null when uncategorised.</summary>
    public Guid? AgendaCategoryKey { get; set; }

    /// <summary>How the item repeats; <c>None</c> stores a single occurrence.</summary>
    public RecurrenceFrequency RecurrenceFrequency { get; set; } = RecurrenceFrequency.None;

    /// <summary>Step size in the frequency's unit — every N weeks/months/years. Always ≥ 1.</summary>
    public int RecurrenceInterval { get; set; } = 1;

    /// <summary>
    /// Weekly-only bitmask of weekdays (bit 0 = Sunday … bit 6 = Saturday). 0 falls back to the start
    /// day. Ignored for monthly/yearly.
    /// </summary>
    public int RecurrenceByWeekday { get; set; }

    /// <summary>Inclusive UTC end of the series; null means open-ended (bounded by the query window).</summary>
    public DateTime? RecurrenceEndUtc { get; set; }

    /// <summary>Max number of occurrences; null means unbounded. Applied together with <see cref="RecurrenceEndUtc"/>.</summary>
    public int? RecurrenceCount { get; set; }

    public Guid CreatedByUserKey { get; set; }

    /// <summary>When the task as a whole became done; cleared if it is reopened. What auto-archiving counts from.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Set while the task sits in the archive: off the board, the dashboard and the calendar, still
    /// findable and restorable. Purged for good after the kurin's retention.
    /// </summary>
    public DateTime? ArchivedAtUtc { get; set; }

    /// <summary>Who archived it; null when it was archived automatically.</summary>
    public Guid? ArchivedByUserKey { get; set; }

    public Kurin Kurin { get; set; } = null!;
    public AgendaCategory? Category { get; set; }
    public ICollection<AgendaAssignment> Assignments { get; set; } = new List<AgendaAssignment>();
    public ICollection<AgendaResponse> Responses { get; set; } = new List<AgendaResponse>();
}
