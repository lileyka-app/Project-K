namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

/// <summary>
/// A picture uploaded into a project's description. The row is what keeps the blob alive: the
/// orphan cleanup sweeps every blob in its folder that no row names, and saving a description
/// drops the rows it no longer mentions.
/// </summary>
public class ProbeProjectImage : Entity
{
    public Guid ProbeProjectImageKey { get; set; } = Guid.NewGuid();
    public Guid ProbeProjectKey { get; set; }
    public ProbeProject? Project { get; set; }
    public string BlobName { get; set; } = string.Empty;

    /// <summary>The bytes the picture took on upload, counted against the person's storage.</summary>
    public long Bytes { get; set; }
}
