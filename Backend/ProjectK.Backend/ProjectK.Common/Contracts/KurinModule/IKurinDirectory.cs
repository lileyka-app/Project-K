using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// What the kurin module tells others about the kurin itself and its гуртки. Tags have their own door,
/// <see cref="IKurinTagDirectory"/>.
/// </summary>
public interface IKurinDirectory
{
    /// <summary>The kurin's гуртки by key, with their names; empty when there is no such kurin.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetGroupNamesAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The гуртки with these keys, by key, wherever they are; dissolved ones too, since a person's
    /// history still names the гурток they led. A key with no гурток is left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GroupSummary>> GetGroupsAsync(IReadOnlyCollection<Guid> groupKeys, CancellationToken cancellationToken = default);

    /// <summary>How long the kurin's done tasks stay on the board and its archive is kept; null when there is no such kurin.</summary>
    Task<KurinTaskArchivePolicy?> GetTaskArchivePolicyAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Whether the kurin keeps точкування on; true for a kurin there is no record of, as every kurin did before the switch.</summary>
    Task<bool> IsScoreEnabledAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>What each of these kurins lets its minors do with their card; a kurin that never said is absent.</summary>
    Task<IReadOnlyCollection<MinorCardSharing>> GetMinorCardSharingAsync(IReadOnlyCollection<Guid> kurinKeys, CancellationToken cancellationToken = default);
}
