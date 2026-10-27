namespace ProjectK.Common.Interfaces.Modules.KurinModule;

/// <summary>Which edition of the probe programme a person's гурток walks, for the probes module.</summary>
public interface IGroupProbeEditions
{
    /// <summary>
    /// The edition chosen by the гурток the person currently stands in within that kurin; null for
    /// the unnamed edition, and when they stand in no гурток.
    /// </summary>
    Task<string?> ForMemberAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default);
}
