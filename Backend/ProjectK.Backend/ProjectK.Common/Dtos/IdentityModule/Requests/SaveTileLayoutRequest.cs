namespace ProjectK.Common.Dtos.IdentityModule.Requests;

public record SaveTileLayoutRequest(IReadOnlyList<string> TileKeys, IReadOnlyList<string>? HiddenTileKeys = null, int SchemaVersion = 1);
