namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>One person in a planning session, with how much their absence weighs and when they are busy.</summary>
/// <param name="MemberKey">The member's key as text, as the planning page has always read it.</param>
/// <param name="FullName">Their name as the session lists it.</param>
/// <param name="RoleWeight">How much their absence weighs against the dates.</param>
/// <param name="BusyRanges">When they cannot come.</param>
public sealed record PlanningParticipantDto(
    string MemberKey,
    string FullName,
    double RoleWeight,
    IReadOnlyList<DateRangeDto> BusyRanges);
