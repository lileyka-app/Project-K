using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>
/// A ступінь (УПЮ, УСП, УПС; a пластун's and a пластунка's apart) or a senior відзначення from the
/// catalogue, with its picture. The УПЮ відзначення are not here: they go by level, as before.
/// </summary>
/// <param name="Group">The catalogue's group: a ступінь's улад, or an award's ladder (<c>usp-vidznachennia</c>, <c>orden-sv-yuriia</c>…).</param>
/// <param name="ImageUrl">The colour picture on the catalogue service; empty when it has none.</param>
public sealed record CatalogHonour(
    string Id,
    HonourKind Kind,
    string Title,
    string Group,
    string Ulad,
    int Order,
    string ImageUrl);
