namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>A вмілість the person holds, confirmed.</summary>
/// <param name="DuringProbe">Confirmed on or after the day the third probe opened.</param>
public sealed record ThirdProbeBadgeDto(
    string BadgeId,
    string Title,
    string? ImagePath,
    string? Specialization,
    DateTime? ConfirmedAtUtc,
    bool DuringProbe);
