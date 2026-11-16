using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <summary>
/// What the person writes into their third-probe file. The confirmation of the reflection and the
/// stages are not here: leaders set those.
/// </summary>
public sealed record ThirdProbeFileInput(
    string? MentorZvyazkovyi,
    string? MentorVporiadnyk,
    string? MentorReferent,
    DateOnly? ReflectionOn,
    string? ReflectionWith,
    ThirdProbeCampKind? CampKind,
    string? CampName,
    DateOnly? CampFrom,
    DateOnly? CampTo,
    string? PhysicalBadgeId,
    string? Resume);
