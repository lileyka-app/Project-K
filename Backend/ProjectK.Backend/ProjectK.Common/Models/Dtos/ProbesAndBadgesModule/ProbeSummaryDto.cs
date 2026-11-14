namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>A проба from the catalogue, without its points.</summary>
/// <param name="Edition">The edition of the programme; null for the one every kurin has walked
/// since before editions were named.</param>
/// <param name="Order">The probe's place in its edition.</param>
/// <param name="StatusCode">"approved" or "trial".</param>
public sealed record ProbeSummaryDto(
    string Id,
    string Title,
    int PointsCount,
    int SectionsCount,
    string? Edition,
    int Order,
    string KraiCode,
    string StatusCode);
