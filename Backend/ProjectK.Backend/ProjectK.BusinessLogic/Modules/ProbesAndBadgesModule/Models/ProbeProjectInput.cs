using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <summary>What the project form sends, as the УПЮ template lays a project out.</summary>
public sealed record ProbeProjectInput(
    string? Title,
    ProbeProjectArea? Area,
    IReadOnlyList<ProbeProjectReach>? Reach,
    bool IsShared,
    string? SharedWith,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Location,
    string? CommandantName,
    string? CommandantContact,
    string? Description,
    string? Goal,
    string? Plan,
    string? Results);
