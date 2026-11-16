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

    /// <summary>How long the kurin's done tasks stay on the board and its archive is kept; null when there is no such kurin.</summary>
    Task<KurinTaskArchivePolicy?> GetTaskArchivePolicyAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
