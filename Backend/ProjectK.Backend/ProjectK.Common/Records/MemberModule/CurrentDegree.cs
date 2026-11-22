using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// The ступінь a person stands at now, and the catalogue's entry it is drawn with — apart for a пластун
/// and a пластунка. <paramref name="CatalogId"/> is null for a level the catalogue has no відзнака for.
/// A старший пластун with a скоб from УПЮ is drawn as the скоб, which <paramref name="CarriedSkob"/> names.
/// </summary>
public sealed record CurrentDegree(PlastLevel Level, string? CatalogId, PlastLevel? CarriedSkob = null);
