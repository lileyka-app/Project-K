namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A camp-date planning session: what was asked, who takes part, and the dates it found.</summary>
/// <param name="KurinKey">The kurin's key as text, as the planning page has always read it.</param>
/// <param name="IsCalculated">Whether the dates below were found; until then they are empty.</param>
/// <param name="CanDelete">Whether the caller may withdraw this session — its author, or whole-kurin management.</param>
public sealed record PlanningSessionDto(
    Guid PlanningSessionKey,
    string Name,
    string KurinKey,
    DateTime SearchStart,
    DateTime SearchEnd,
    int DurationDays,
    bool IsCalculated,
    DateTime? OptimalStartDate,
    DateTime? OptimalEndDate,
    double ConflictScore,
    bool CanDelete,
    IReadOnlyList<PlanningParticipantDto> Participants);
