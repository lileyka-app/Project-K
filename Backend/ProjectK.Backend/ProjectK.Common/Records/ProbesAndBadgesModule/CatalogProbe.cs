namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>
/// A probe as the catalogue describes it, reduced to what somebody writing progress rows needs:
/// which probe, and which points it has, in the order the catalogue lists them.
/// </summary>
public sealed record CatalogProbe(string Id, string Title, IReadOnlyList<string> PointIds);
