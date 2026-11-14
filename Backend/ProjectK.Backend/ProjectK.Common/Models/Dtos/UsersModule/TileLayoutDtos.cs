namespace ProjectK.Common.Models.Dtos.UsersModule;

public record TileLayoutDto(
    string BoardKey,
    IReadOnlyList<string> TileKeys,
    IReadOnlyList<string> HiddenTileKeys,
    int SchemaVersion,
    DateTime UpdatedAtUtc);

public record SaveTileLayoutRequestDto(IReadOnlyList<string> TileKeys, IReadOnlyList<string>? HiddenTileKeys = null, int SchemaVersion = 1);
