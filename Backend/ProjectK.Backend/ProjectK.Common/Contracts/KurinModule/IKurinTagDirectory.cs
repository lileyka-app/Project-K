using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// What the kurin module tells others about its tags: whether the kurin uses them, which it has, who
/// carries them, and how far the current viewer sees who carries one. Tags are the kurin's, so the
/// kurin answers all of it.
/// </summary>
public interface IKurinTagDirectory
{
    /// <summary>Whether the kurin has tags on, its default filled in; false when there is no such kurin.</summary>
    Task<bool> IsEnabledAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// How far the current viewer sees tags on others in the kurin: the whole kurin for whoever manages
    /// them, otherwise the kurin's <c>SeeTags</c> row; nobody while tags are off. One's own tags are not
    /// counted here.
    /// </summary>
    Task<CapabilityReach> GetCarrierReachAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The kurin's tags, by name, whether tags are on or not.</summary>
    Task<IReadOnlyList<KurinTagSummary>> GetTagsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The kurin's tags one person carries now.</summary>
    Task<IReadOnlyList<Guid>> GetTagKeysOfMemberAsync(Guid kurinKey, Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everyone carrying each of these tags who is in its kurin now, by tag; a tag nobody carries maps
    /// to an empty list.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCarriersAsync(
        IReadOnlyCollection<Guid> kurinTagKeys,
        CancellationToken cancellationToken = default);
}
