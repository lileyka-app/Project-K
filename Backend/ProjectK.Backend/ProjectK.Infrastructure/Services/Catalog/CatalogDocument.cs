using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

// The public catalogue document, schema version 2 (GET /v1/catalog). Only the fields Lilejka reads.

internal sealed record CatalogDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("revision")] long Revision,
    [property: JsonPropertyName("categories")] IReadOnlyList<CatalogCategory>? Categories,
    [property: JsonPropertyName("badges")] IReadOnlyList<CatalogBadge>? Badges,
    [property: JsonPropertyName("probes")] IReadOnlyList<CatalogProbe>? Probes,
    [property: JsonPropertyName("awards")] IReadOnlyList<CatalogAward>? Awards)
{
    [JsonPropertyName("editions")] public IReadOnlyList<CatalogEdition>? Editions { get; init; }
    [JsonPropertyName("degrees")] public IReadOnlyList<CatalogDegree>? Degrees { get; init; }
}
