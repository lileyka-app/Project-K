using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// What the kurin module tells others about its tags: whether the kurin uses them, and how far the
/// current viewer sees who carries one. Tags are the kurin's, so the kurin answers both.
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
}
