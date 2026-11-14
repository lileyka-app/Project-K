namespace ProjectK.Common.Dtos.UsersModule;

public record SaveTileLayoutRequestDto(IReadOnlyList<string> TileKeys, IReadOnlyList<string>? HiddenTileKeys = null, int SchemaVersion = 1);
