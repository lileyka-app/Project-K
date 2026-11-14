using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// An agenda item as the calendar and board see it. <see cref="CanEdit"/> and
/// <see cref="CanChangeStatus"/> are resolved per viewer so the UI can enable drag/edit without a
/// second round-trip.
/// </summary>
public sealed record AgendaItemDto
{
    public required Guid AgendaItemKey { get; set; }
    public required Guid KurinKey { get; set; }
    public required AgendaItemKind Kind { get; set; }
    public required string Title { get; set; }
    public required string? Description { get; set; }
    public required string? Location { get; set; }

    /// <summary>The task as a whole, over all its targets.</summary>
    public required AgendaItemStatus Status { get; set; }

    /// <summary>The column this viewer sees it in: their own part's state, or the whole task's when nothing on it is theirs.</summary>
    public required AgendaItemStatus ViewerStatus { get; set; }
    public required DateTime? StartUtc { get; set; }
    public required DateTime? EndUtc { get; set; }
    public required bool IsAllDay { get; set; }
    public required Guid CreatedByUserKey { get; set; }
    public required string? CreatedByName { get; set; }
    public required DateTime CreatedUtc { get; set; }
    public required DateTime UpdatedUtc { get; set; }

    /// <summary>When the task as a whole was closed; what auto-archiving counts from.</summary>
    public required DateTime? CompletedAtUtc { get; set; }

    /// <summary>Set for a task in the archive.</summary>
    public required DateTime? ArchivedAtUtc { get; set; }

    /// <summary>Who archived it; null with <see cref="ArchivedAtUtc"/> set means it was archived automatically.</summary>
    public required string? ArchivedByName { get; set; }
    public required bool CanEdit { get; set; }

    /// <summary>Whether a drag on the board moves anything for this viewer.</summary>
    public required bool CanChangeStatus { get; set; }
    /// <summary>False when the viewer sees the item only because they raised it: it is someone else's to do.</summary>
    public required bool AddressedToViewer { get; set; }

    /// <summary>Event group, resolved for display; null when the item is uncategorised.</summary>
    public required Guid? CategoryKey { get; set; }
    public required string? CategoryName { get; set; }
    public required string? CategoryColorHex { get; set; }
    public required string? CategoryIcon { get; set; }

    /// <summary>The item's group is «графік куреня» — the calendar names the гурток on its plate.</summary>
    public required bool IsKurinSchedule { get; set; }

    /// <summary>
    /// Why it is on this viewer's calendar. <see cref="AgendaAudience.Schedule"/>: only through the
    /// kurin's schedule — nothing to answer, and the dialog shows it read-only.
    /// </summary>
    public required AgendaAudience Audience { get; set; }

    /// <summary>Recurrence rule echoed back so the edit dialog can repopulate the series settings.</summary>
    public required RecurrenceFrequency RecurrenceFrequency { get; set; }
    public required int RecurrenceInterval { get; set; }
    public required int RecurrenceByWeekday { get; set; }
    public required DateTime? RecurrenceEndUtc { get; set; }
    public required int? RecurrenceCount { get; set; }

    /// <summary>True when this row is one expanded occurrence of a series (its dates differ from the stored item).</summary>
    public required bool IsRecurrenceInstance { get; set; }

    /// <summary>The series' stored (unshifted) start/end, so a dragged occurrence can move the whole series by its delta.</summary>
    public required DateTime? SeriesStartUtc { get; set; }
    public required DateTime? SeriesEndUtc { get; set; }

    public required List<AgendaAssignmentDto> Assignments { get; set; }
}
