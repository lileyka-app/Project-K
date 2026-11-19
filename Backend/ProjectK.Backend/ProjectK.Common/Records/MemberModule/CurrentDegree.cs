using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// The ступінь a person stands at now, and the catalogue's entry for it — drawn apart for a пластун
/// and a пластунка. <paramref name="CatalogId"/> is null for a level the catalogue has no відзнака for.
/// </summary>
public sealed record CurrentDegree(PlastLevel Level, string? CatalogId);
