namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>A проба from the catalogue with its points grouped into sections.</summary>
public sealed record GroupedProbeDto(
    string Id,
    string Title,
    int PointsCount,
    int SectionsCount,
    IReadOnlyList<ProbeSection> Sections);
