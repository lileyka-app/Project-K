namespace ProjectK.Common.Dtos.ScoreModule;

/// <summary>What became of each person a mark was asked for.</summary>
public sealed record MarkAttendanceResultDto
{
    public required Guid MembershipKey { get; init; }
    /// <summary><c>Marked</c> now, or <c>AlreadyMarked</c> by someone before.</summary>
    public required string Outcome { get; init; }
    public required string? MarkedByName { get; init; }
}
