using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// An agenda item as the calendar and board see it. <see cref="CanEdit"/> and
/// <see cref="CanChangeStatus"/> are resolved per viewer so the UI can enable drag/edit without a
/// second round-trip.
/// </summary>
public record AgendaItemResponse
{
    public Guid AgendaItemKey { get; set; }
    public Guid KurinKey { get; set; }
    public AgendaItemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }

    /// <summary>The task as a whole, over all its targets.</summary>
    public AgendaItemStatus Status { get; set; }

    /// <summary>The column this viewer sees it in: their own part's state, or the whole task's when nothing on it is theirs.</summary>
    public AgendaItemStatus ViewerStatus { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public Guid CreatedByUserKey { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>When the task as a whole was closed; what auto-archiving counts from.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Set for a task in the archive.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    /// <summary>Who archived it; null with <see cref="ArchivedAtUtc"/> set means it was archived automatically.</summary>
    public string? ArchivedByName { get; set; }
    public bool CanEdit { get; set; }

    /// <summary>Whether a drag on the board moves anything for this viewer.</summary>
    public bool CanChangeStatus { get; set; }
    /// <summary>False when the viewer sees the item only because they raised it: it is someone else's to do.</summary>
    public bool AddressedToViewer { get; set; }

    /// <summary>Event group, resolved for display; null when the item is uncategorised.</summary>
    public Guid? CategoryKey { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColorHex { get; set; }
    public string? CategoryIcon { get; set; }

    /// <summary>The item's group is «графік куреня» — the calendar names the гурток on its plate.</summary>
    public bool IsKurinSchedule { get; set; }

    /// <summary>
    /// Why it is on this viewer's calendar. <see cref="AgendaAudience.Schedule"/>: only through the
    /// kurin's schedule — nothing to answer, and the dialog shows it read-only.
    /// </summary>
    public AgendaAudience Audience { get; set; }

    /// <summary>Recurrence rule echoed back so the edit dialog can repopulate the series settings.</summary>
    public RecurrenceFrequency RecurrenceFrequency { get; set; }
    public int RecurrenceInterval { get; set; } = 1;
    public int RecurrenceByWeekday { get; set; }
    public DateTime? RecurrenceEndUtc { get; set; }
    public int? RecurrenceCount { get; set; }

    /// <summary>True when this row is one expanded occurrence of a series (its dates differ from the stored item).</summary>
    public bool IsRecurrenceInstance { get; set; }

    /// <summary>The series' stored (unshifted) start/end, so a dragged occurrence can move the whole series by its delta.</summary>
    public DateTime? SeriesStartUtc { get; set; }
    public DateTime? SeriesEndUtc { get; set; }

    public List<AgendaAssignmentDto> Assignments { get; set; } = [];
}

/// <summary>The board, one page per column, and the targets its filter can offer.</summary>
public record AgendaBoardResponse
{
    public List<AgendaBoardColumnDto> Columns { get; set; } = [];
    public List<AgendaBoardTargetDto> Targets { get; set; } = [];
}

public record AgendaBoardColumnDto
{
    public AgendaItemStatus Status { get; set; }

    /// <summary>How many tasks the column holds under the filter — the «Завантажити ще» counts against it.</summary>
    public int Total { get; set; }

    public List<AgendaItemResponse> Items { get; set; } = [];
}

public record AgendaBoardTargetDto
{
    public AgendaTargetType TargetType { get; set; }
    public Guid TargetKey { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>A page of the archive, newest first, and how long the kurin keeps it.</summary>
public record AgendaArchivePageResponse
{
    public int Total { get; set; }
    public List<AgendaItemResponse> Items { get; set; } = [];
    public AgendaArchivePolicyDto Policy { get; set; } = new();
}

/// <summary>A kurin's archive rules; an empty period switches that step off.</summary>
public record AgendaArchivePolicyDto
{
    public int? AutoArchiveAfterDays { get; set; }
    public int? PurgeAfterDays { get; set; }
}

public record AgendaAssignmentDto
{
    public Guid AgendaAssignmentKey { get; set; }
    public AgendaTargetType TargetType { get; set; }
    public Guid TargetKey { get; set; }

    /// <summary>Human label for the target (kurin number, group name, member full name).</summary>
    public string? Label { get; set; }

    public AgendaCompletionMode CompletionMode { get; set; }

    /// <summary>The target's state; in «кожному окремо» what its people's parts add up to now.</summary>
    public AgendaItemStatus Status { get; set; }

    /// <summary>Who last moved it and when — never a «done» without a name.</summary>
    public string? StatusChangedByName { get; set; }
    public DateTime? StatusChangedAtUtc { get; set; }

    /// <summary>Whether this viewer may move the target's single state (never in «кожному окремо»).</summary>
    public bool CanChangeStatus { get; set; }

    /// <summary>«Кожному окремо» only: how many of the people in the target are done, of how many.</summary>
    public int? DoneCount { get; set; }
    public int? PeopleCount { get; set; }

    /// <summary>
    /// «Кожному окремо», for those who run the target: everyone in it with their part. Others see the
    /// counts and their own part, not the list — a youth has no need of who else has not paid.
    /// </summary>
    public List<AgendaPartDto>? Parts { get; set; }
}

/// <summary>One person's part of a target done «кожному окремо».</summary>
public record AgendaPartDto
{
    public Guid MemberKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public AgendaItemStatus Status { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime? ChangedAtUtc { get; set; }
    public bool CanChangeStatus { get; set; }
}

/// <summary>
/// The "Assign for" tree, flagged by what the kurin's policy lets the current user target: each kind of
/// target (the kurin, a гурток, a провід, a person) has its own row. A гурток is listed when anything
/// in it may be picked.
/// </summary>
public record AgendaAssignTargetsResponse
{
    public bool CanTargetKurin { get; set; }
    public Guid KurinKey { get; set; }
    public string KurinLabel { get; set; } = string.Empty;

    /// <summary>Kurin-level проводи the viewer may target: КВ and Курінний провід.</summary>
    public List<AgendaLeadershipTargetDto> KurinLeaderships { get; set; } = [];
    public List<AgendaGroupTargetDto> Groups { get; set; } = [];
}

public record AgendaGroupTargetDto
{
    public Guid GroupKey { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>False when the group is shown only as a container for its members (no group-level target right).</summary>
    public bool CanTargetGroup { get; set; }

    /// <summary>The group's Гуртковий провід, when it has an active office; null otherwise.</summary>
    public AgendaLeadershipTargetDto? Leadership { get; set; }
    public List<AgendaMemberTargetDto> Members { get; set; } = [];
}

public record AgendaLeadershipTargetDto
{
    public Guid LeadershipKey { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool CanTarget { get; set; }
}

public record AgendaMemberTargetDto
{
    public Guid MemberKey { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>False when the person is listed only beside the гурток or провід the viewer may pick.</summary>
    public bool CanTarget { get; set; }
}
