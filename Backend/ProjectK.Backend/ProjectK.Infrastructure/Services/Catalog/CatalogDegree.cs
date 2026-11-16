using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>A ступінь: one per улад and gender, in the order they are taken.</summary>
internal sealed record CatalogDegree(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("ulad")] string? Ulad,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("images")] CatalogImages? Images);
