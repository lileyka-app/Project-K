namespace ProjectK.Common.Models.Dtos.ScoreModule;

/// <summary>Who said an occurrence did not take place, and when.</summary>
public sealed record OccurrenceNotHeldDto
{
    public required string? MarkedByName { get; init; }
    public required DateTime MarkedAtUtc { get; init; }
}
