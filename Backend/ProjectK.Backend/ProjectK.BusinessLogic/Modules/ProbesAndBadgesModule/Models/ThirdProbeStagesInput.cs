namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <summary>The referent's stages, each the day it happened; null where it has not.</summary>
public sealed record ThirdProbeStagesInput(
    DateOnly? MotivationLetterOn,
    DateOnly? SelfReflectionOn,
    DateOnly? ApplicationSubmittedOn,
    DateOnly? OpenedOn,
    DateOnly? RecommendedOn,
    DateOnly? ClosingSubmittedOn,
    DateOnly? NamedOn,
    DateOnly? FrozenOn,
    DateOnly? ArchivedOn);
