using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

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
