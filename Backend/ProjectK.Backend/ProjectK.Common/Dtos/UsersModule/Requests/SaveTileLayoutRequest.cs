namespace ProjectK.Common.Dtos.UsersModule.Requests;

public record SaveTileLayoutRequest(IReadOnlyList<string> TileKeys, IReadOnlyList<string>? HiddenTileKeys = null, int SchemaVersion = 1);
