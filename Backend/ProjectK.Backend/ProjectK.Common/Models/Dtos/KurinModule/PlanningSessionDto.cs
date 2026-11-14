namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A camp-date planning session: what was asked, who takes part, and the dates it found.</summary>
/// <param name="PlanningSessionKey">The session.</param>
/// <param name="Name">What the session is called.</param>
/// <param name="KurinKey">The kurin's key as text, as the planning page has always read it.</param>
/// <param name="SearchStart">The first day the camp may start.</param>
/// <param name="SearchEnd">The last day the camp may end.</param>
/// <param name="DurationDays">How long the camp lasts.</param>
/// <param name="IsCalculated">Whether the dates below were found; until then they are empty.</param>
/// <param name="OptimalStartDate">The start the search found best.</param>
/// <param name="OptimalEndDate">The end that start gives.</param>
/// <param name="ConflictScore">How much absence those dates cost; lower is better.</param>
/// <param name="CanDelete">Whether the caller may withdraw this session — its author, or whole-kurin management.</param>
/// <param name="Participants">Who takes part, with when they are busy.</param>
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
