using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

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
