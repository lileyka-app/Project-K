using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

internal sealed record CatalogCategory(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);
