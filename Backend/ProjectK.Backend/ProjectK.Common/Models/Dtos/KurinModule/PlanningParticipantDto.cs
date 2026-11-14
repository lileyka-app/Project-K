namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>One person in a planning session, with how much their absence weighs and when they are busy.</summary>
/// <param name="MemberKey">The member's key as text, as the planning page has always read it.</param>
public sealed record PlanningParticipantDto(
    string MemberKey,
    string FullName,
    double RoleWeight,
    IReadOnlyList<DateRangeDto> BusyRanges);
