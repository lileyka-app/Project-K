using System.Text.Json;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;

/// <summary>
/// A stored board layout and its answer. The tile lists live in the row as JSON arrays; a value that
/// does not read back as one is an empty list, so a damaged row falls back to the default board.
/// </summary>
public static class TileLayoutMapping
{
    public static TileLayoutDto ToDto(this UserTileLayout layout) => new(
        layout.BoardKey,
        ReadKeys(layout.TileOrderJson),
        ReadKeys(layout.HiddenTilesJson),
        layout.SchemaVersion,
        layout.UpdatedAtUtc);

    /// <summary>Writes the lists, the schema and the time. The owner and the board stay as they are.</summary>
    public static void ApplyTo(this SaveTileLayoutCommand request, UserTileLayout layout, DateTime now)
    {
        layout.TileOrderJson = WriteKeys(request.TileKeys ?? []);
        layout.HiddenTilesJson = WriteKeys(request.HiddenTileKeys ?? []);
        layout.SchemaVersion = request.SchemaVersion <= 0 ? 1 : request.SchemaVersion;
        layout.UpdatedAtUtc = now;
    }

    public static IReadOnlyList<string> ReadKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string WriteKeys(IReadOnlyList<string> tileKeys) => JsonSerializer.Serialize(tileKeys);
}
