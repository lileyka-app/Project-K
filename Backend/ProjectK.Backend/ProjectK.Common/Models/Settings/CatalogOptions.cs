namespace ProjectK.Common.Models.Settings;

/// <summary>
/// Where Lilejka reads вмілості, проби and відзначення from: the catalogue service's public API
/// (<c>GET /v1/catalog</c>). The last good answer is kept on disk, so a restart without the network
/// still has a catalogue.
/// </summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public string BaseUrl { get; set; } = "https://catalog.lileyka.app";

    /// <summary>Only this krai's entries (<c>?krai=</c>); empty takes the whole catalogue.</summary>
    public string? Krai { get; set; }

    public int RefreshMinutes { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>The on-disk copy of the last good answer; relative paths are under the app directory.</summary>
    public string SnapshotPath { get; set; } = "catalog-cache/catalog.json";
}
