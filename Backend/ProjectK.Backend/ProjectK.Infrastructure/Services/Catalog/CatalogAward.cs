using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.Catalog;

internal sealed record CatalogAward(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("level")] int? Level,
    [property: JsonPropertyName("images")] CatalogImages? Images)
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("ulad")] public string? Ulad { get; init; }
}
