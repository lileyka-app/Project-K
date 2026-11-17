namespace ProjectK.Common.Dtos.IdentityModule;

public record TileLayoutDto(
    string BoardKey,
    IReadOnlyList<string> TileKeys,
    IReadOnlyList<string> HiddenTileKeys,
    int SchemaVersion,
    DateTime UpdatedAtUtc);
