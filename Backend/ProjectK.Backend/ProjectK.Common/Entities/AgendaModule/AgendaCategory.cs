using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.AgendaModule;

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
}
