namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record AttendanceMarkDto
{
    public required string? MarkedByName { get; init; }
    public required DateTime MarkedAtUtc { get; init; }
}
