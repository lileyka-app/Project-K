namespace ProjectK.Common.Entities.IdentityModule;

public class UserTileLayout
{
    public Guid UserTileLayoutKey { get; set; }
    public Guid UserKey { get; set; }
    public string BoardKey { get; set; } = string.Empty;
    public string TileOrderJson { get; set; } = "[]";

    /// <summary>Tiles the person took off the board, as a JSON list of keys. Order is kept apart, so hiding never reorders.</summary>
    public string HiddenTilesJson { get; set; } = "[]";
    public int SchemaVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
