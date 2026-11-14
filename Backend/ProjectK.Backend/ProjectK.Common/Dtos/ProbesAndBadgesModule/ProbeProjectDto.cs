using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>One project of the third probe, laid out as the УПЮ template asks.</summary>
/// <param name="Description">Markdown, like the goal, plan and results; pictures are
/// <c>project-image:{key}</c>, their links in <paramref name="Images"/>.</param>
public sealed record ProbeProjectDto(
    Guid ProjectKey,
    string Title,
    ProbeProjectArea? Area,
    IReadOnlyList<ProbeProjectReach> Reach,
    bool IsShared,
    string? SharedWith,
    bool IsSpare,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Location,
    string? EventLeads,
    string? CoordinatorName,
    string? CoordinatorContact,
    string Description,
    string Goal,
    string Plan,
    string Results,
    DateTime? ApprovedAtUtc,
    string? ApprovedByName,
    DateTime? CompletedAtUtc,
    string? CompletedByName,
    string? CoordinatorComment,
    DateTime UpdatedAtUtc,
    string? UpdatedByName,
    IReadOnlyList<ProbeProjectImageDto> Images);
