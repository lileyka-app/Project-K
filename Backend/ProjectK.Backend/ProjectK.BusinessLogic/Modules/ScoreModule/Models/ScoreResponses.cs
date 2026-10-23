using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Models;

public sealed class ScorePeriodDto
{
    /// <summary><c>Year</c> or <c>Stage</c>.</summary>
    public string Kind { get; set; } = string.Empty;
    public int? Year { get; set; }
    public Guid? StageKey { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public sealed class ScorePeriodsDto
{
    public IReadOnlyList<ScorePeriodDto> Years { get; set; } = [];
    public IReadOnlyList<ScorePeriodDto> Stages { get; set; } = [];
}

public sealed class ScoreItemDto
{
    public Guid ScoreItemKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ScoreEntryDto
{
    public Guid ScoreEntryKey { get; set; }
    public Guid? MembershipKey { get; set; }
    public Guid? MemberKey { get; set; }
    public string? MemberName { get; set; }
    public Guid? GroupKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    /// <summary>Given to the гурток as a whole, not to a person in it.</summary>
    public bool IsForGroup { get; set; }
    public Guid? ScoreItemKey { get; set; }
    public string? ItemName { get; set; }
    public int Points { get; set; }
    public string? Reason { get; set; }
    public Guid? AgendaItemKey { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>One гурток's row in the kurin's table.</summary>
public sealed class ScoreGroupRowDto
{
    public Guid GroupKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int Place { get; set; }
    public decimal Score { get; set; }
    public decimal OtherScore { get; set; }
    public int YouthPoints { get; set; }
    public decimal YouthCount { get; set; }
    public decimal Average { get; set; }
    public int GroupPoints { get; set; }
    /// <summary>Whether the caller may open the гурток's own page.</summary>
    public bool CanOpen { get; set; }
}

public sealed class KurinScoreViewerDto
{
    /// <summary>May score somewhere in the kurin — a sheet is theirs to open.</summary>
    public bool CanScore { get; set; }
    public bool CanManage { get; set; }
}

public sealed class KurinScoreResponse
{
    public Guid KurinKey { get; set; }
    public ScoreAlgorithm Algorithm { get; set; }
    public ScorePeriodDto Period { get; set; } = new();
    public ScorePeriodsDto Periods { get; set; } = new();
    public IReadOnlyList<ScoreGroupRowDto> Groups { get; set; } = [];
    public KurinScoreViewerDto Viewer { get; set; } = new();
}

public sealed class ScoreRuleDto
{
    public ScoreSource Source { get; set; }
    public int Variant { get; set; }
    public DateOnly FromDate { get; set; }
    public int Points { get; set; }
}

public sealed class ScoreAttendanceRateDto
{
    public Guid? AgendaCategoryKey { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColorHex { get; set; }
    public string? CategoryIcon { get; set; }
    public int Points { get; set; }
}

public sealed class ScoreStageDto
{
    public Guid ScoreStageKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

/// <summary>Everything the суддя куреня sets: how the kurin scores and what things are worth.</summary>
public sealed class KurinScoreSettingsResponse
{
    public Guid KurinKey { get; set; }
    public ScoreAlgorithm Algorithm { get; set; }
    /// <summary>Every group of events, with what being at one of its events is worth (0 when nothing set).</summary>
    public IReadOnlyList<ScoreAttendanceRateDto> AttendanceRates { get; set; } = [];
    public IReadOnlyList<ScoreRuleDto> Rules { get; set; } = [];
    public IReadOnlyList<ScoreItemDto> Items { get; set; } = [];
    public IReadOnlyList<ScoreStageDto> Stages { get; set; } = [];
}

public sealed class ScorePersonRowDto
{
    public Guid MembershipKey { get; set; }
    public Guid MemberKey { get; set; }
    public string FullName { get; set; } = string.Empty;
    /// <summary><c>Current</c> in the гурток, <c>Moved</c> to another, or <c>Left</c> the kurin.</summary>
    public string Standing { get; set; } = string.Empty;
    public int Total { get; set; }
    public IReadOnlyDictionary<ScoreSource, int> BySource { get; set; } = new Dictionary<ScoreSource, int>();
}

public sealed class GroupScoreResponse
{
    public Guid GroupKey { get; set; }
    public Guid KurinKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public ScoreAlgorithm Algorithm { get; set; }
    public ScorePeriodDto Period { get; set; } = new();
    public ScorePeriodsDto Periods { get; set; } = new();
    public ScoreGroupRowDto Standing { get; set; } = new();
    public int GroupCount { get; set; }
    public IReadOnlyList<ScorePersonRowDto> People { get; set; } = [];
    public IReadOnlyList<ScoreEntryDto> Entries { get; set; } = [];
    public IReadOnlyList<ScoreItemDto> Items { get; set; } = [];
    public bool CanScore { get; set; }
}

public sealed class AttendanceMarkDto
{
    public string? MarkedByName { get; set; }
    public DateTime MarkedAtUtc { get; set; }
}

public sealed class SheetPersonDto
{
    public Guid MembershipKey { get; set; }
    public Guid MemberKey { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid? GroupKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    /// <summary>The person's answer to the invitation, if they gave one.</summary>
    public AgendaRsvpStatus? Rsvp { get; set; }
    /// <summary>Whether the event was aimed at them — at the kurin, their гурток or them by name.</summary>
    public bool IsAssigned { get; set; }
    public AttendanceMarkDto? Attendance { get; set; }
    /// <summary>Whether the caller may mark or score this person.</summary>
    public bool CanScore { get; set; }
    public IReadOnlyList<ScoreEntryDto> Entries { get; set; } = [];
}

public sealed class SheetGroupDto
{
    public Guid GroupKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public bool CanScore { get; set; }
    public IReadOnlyList<ScoreEntryDto> Entries { get; set; } = [];
}

/// <summary>Who was at one occurrence of an event, and what was given there.</summary>
public sealed class AttendanceSheetResponse
{
    public Guid KurinKey { get; set; }
    public Guid AgendaItemKey { get; set; }
    public DateTime OccurrenceStartUtc { get; set; }
    public DateTime? OccurrenceEndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsRecurring { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryKey { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColorHex { get; set; }
    public string? CategoryIcon { get; set; }
    /// <summary>What a mark here is worth now.</summary>
    public int AttendancePoints { get; set; }
    /// <summary>Whether the event has a rate of its own, over its group's.</summary>
    public bool HasOwnRate { get; set; }
    public IReadOnlyList<SheetPersonDto> People { get; set; } = [];
    public IReadOnlyList<SheetGroupDto> Groups { get; set; } = [];
    public IReadOnlyList<ScoreItemDto> Items { get; set; } = [];
    public bool CanManage { get; set; }
    /// <summary>Set when someone said the occurrence did not take place; nobody is marked then.</summary>
    public OccurrenceNotHeldDto? NotHeld { get; set; }
}

/// <summary>Who said an occurrence did not take place, and when.</summary>
public sealed class OccurrenceNotHeldDto
{
    public string? MarkedByName { get; set; }
    public DateTime MarkedAtUtc { get; set; }
}

/// <summary>What became of each person a mark was asked for.</summary>
public sealed class MarkAttendanceResultDto
{
    public Guid MembershipKey { get; set; }
    /// <summary><c>Marked</c> now, or <c>AlreadyMarked</c> by someone before.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? MarkedByName { get; set; }
}
