using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

internal sealed record CatalogImages(
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("bw")] string? Bw);
