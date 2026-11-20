namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>
/// The stages the referent of the third probe tracks, each the day it happened; null until then.
/// The same shape travels to the referent's own tool (<see cref="ThirdProberStatusDto"/>).
/// </summary>
public sealed record ThirdProbeStagesDto(
    DateOnly? MotivationLetterOn,
    DateOnly? SelfReflectionOn,
    DateOnly? ApplicationSubmittedOn,
    DateOnly? OpenedOn,
    DateOnly? RecommendedOn,
    DateOnly? ClosingSubmittedOn,
    DateOnly? NamedOn,
    DateOnly? FrozenOn,
    DateOnly? ArchivedOn,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);
