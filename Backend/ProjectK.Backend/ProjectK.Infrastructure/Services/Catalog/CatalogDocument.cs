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
}

/// <summary>An edition of the probes and the name the catalogue gives it for people.</summary>
internal sealed record CatalogEdition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);

internal sealed record CatalogCategory(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);

internal sealed record CatalogImages(
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("bw")] string? Bw);

internal sealed record CatalogRequirement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text);

internal sealed record CatalogBadge(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("level")] int? Level,
    [property: JsonPropertyName("categories")] IReadOnlyList<string>? Categories,
    [property: JsonPropertyName("ulady")] IReadOnlyList<string>? Ulady,
    [property: JsonPropertyName("krai")] string? Krai,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("images")] CatalogImages? Images,
    [property: JsonPropertyName("requirements")] IReadOnlyList<CatalogRequirement>? Requirements,
    [property: JsonPropertyName("instructorRequirements")] string? InstructorRequirements,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("updatedOn")] string? UpdatedOn);

internal sealed record CatalogProbe(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("sections")] IReadOnlyList<CatalogProbeSection>? Sections,
    [property: JsonPropertyName("points")] IReadOnlyList<CatalogRequirement>? Points)
{
    [JsonPropertyName("krai")] public string? Krai { get; init; }
    [JsonPropertyName("edition")] public string? Edition { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

internal sealed record CatalogProbeSection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("points")] IReadOnlyList<CatalogRequirement>? Points);

internal sealed record CatalogAward(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("level")] int? Level,
    [property: JsonPropertyName("images")] CatalogImages? Images);
