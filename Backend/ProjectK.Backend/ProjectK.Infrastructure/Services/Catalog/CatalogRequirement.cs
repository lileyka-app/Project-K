using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

internal sealed record CatalogRequirement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text);
