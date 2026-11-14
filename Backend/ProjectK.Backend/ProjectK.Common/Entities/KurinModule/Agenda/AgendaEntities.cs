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

/// <summary>
/// A per-kurin event group (табір, захід, сходини…), curated by the Зв'язковий. Carries the visual
/// identity (colour + icon) and the defaults an event in the group inherits: capacity with an optional
/// waitlist, a description template, a default duration, whether an RSVP is expected, and a reminder lead.
/// </summary>
public class AgendaCategory : Entity
{
    public Guid AgendaCategoryKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Brand-token colour (hex) used to tint the group's events on the calendar.</summary>
    public string ColorHex { get; set; } = string.Empty;

    /// <summary>Icon name (optimus/pi icon) shown on the group's events to tell табір from захід at a glance.</summary>
    public string? Icon { get; set; }

    /// <summary>Max confirmed attendees; null means unlimited. When set, extra «Going» RSVPs form a waitlist.</summary>
    public int? Capacity { get; set; }
    public bool WaitlistEnabled { get; set; }

    /// <summary>Pre-filled description an event in this group starts from.</summary>
    public string? DefaultDescription { get; set; }

    /// <summary>Whether events in this group ask attendees for an RSVP.</summary>
    public bool RsvpRequired { get; set; }

    /// <summary>Default event length in minutes, used to pre-fill the end when creating an event.</summary>
    public int? DefaultDurationMinutes { get; set; }

    /// <summary>Minutes before start to remind attendees; null disables reminders for the group.</summary>
    public int? ReminderLeadMinutes { get; set; }

    /// <summary>Archived groups stay for historical items but are hidden from the picker.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// «Графік куреня»: the group's events are seen by everyone in the kurin, not only by those they
    /// are assigned to — read-only, with no RSVP and no notification. What makes every гурток's
    /// сходини one schedule.
    /// </summary>
    public bool IsKurinSchedule { get; set; }

    public Kurin Kurin { get; set; } = null!;
}

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

/// <summary>
/// One target an agenda item is assigned to. An item carries many of these so it can be aimed at the
/// kurin, several groups and individual members simultaneously.
/// </summary>
public class AgendaAssignment : Entity
{
    public Guid AgendaAssignmentKey { get; set; } = Guid.NewGuid();
    public Guid AgendaItemKey { get; set; }
    public AgendaTargetType TargetType { get; set; }

    /// <summary>KurinKey, GroupKey or MemberKey, per <see cref="TargetType"/>.</summary>
    public Guid TargetKey { get; set; }

    /// <summary>Whether the target does the task once together or each person their own. Changeable only while nothing has moved.</summary>
    public AgendaCompletionMode CompletionMode { get; set; } = AgendaCompletionMode.Shared;

    /// <summary>
    /// The target's own state. Set directly in the shared modes; in <see cref="AgendaCompletionMode.PerMember"/>
    /// derived from <see cref="Progress"/> over the people in the target whenever a part moves.
    /// </summary>
    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.Todo;

    public Guid? StatusChangedByUserKey { get; set; }

    public DateTime? StatusChangedAtUtc { get; set; }

    public AgendaItem AgendaItem { get; set; } = null!;

    /// <summary>One row per person who has moved their part, in <see cref="AgendaCompletionMode.PerMember"/>; no row is Todo.</summary>
    public ICollection<AgendaAssignmentProgress> Progress { get; set; } = new List<AgendaAssignmentProgress>();
}

/// <summary>
/// One person's part of a task done «кожному окремо». A row appears the first time the part moves; a
/// person with no row has not started. The people counted are those in the target when it is read,
/// so a newcomer starts at Todo and a leaver's row stays as history without counting.
/// </summary>
public class AgendaAssignmentProgress : Entity
{
    public Guid AgendaAssignmentProgressKey { get; set; } = Guid.NewGuid();
    public Guid AgendaAssignmentKey { get; set; }
    public Guid MemberKey { get; set; }
    public AgendaItemStatus Status { get; set; }

    /// <summary>Who moved it — the person, or their провід marking it for them.</summary>
    public Guid ChangedByUserKey { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public AgendaAssignment Assignment { get; set; } = null!;
}
