using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>An edition of the probes and the name the catalogue gives it for people.</summary>
internal sealed record CatalogEdition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);
