using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

internal sealed record CatalogProbeSection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("points")] IReadOnlyList<CatalogRequirement>? Points);
