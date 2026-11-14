namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>
/// A проба from the catalogue, without its points. <see cref="Edition"/> is the edition of the
/// programme, null for the one every kurin has walked since before editions were named;
/// <see cref="Order"/> is the probe's place in it; <see cref="StatusCode"/> is "approved" or "trial".
/// </summary>
public sealed record ProbeSummaryDto(
    string Id,
    string Title,
    int PointsCount,
    int SectionsCount,
    string? Edition,
    int Order,
    string KraiCode,
    string StatusCode);
