using ProjectK.BusinessLogic.Modules.KurinModule.Models;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>Whose tags the viewer sees, and the kurin's tag switches. See <see cref="KurinTagVisibility"/>.</summary>
public interface IKurinTagVisibility
{
    /// <summary>The kurin's switches with its defaults filled in; null when there is no such kurin.</summary>
    Task<KurinTagSettingsState?> SettingsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Whether the viewer may define and put on tags in the kurin.</summary>
    Task<bool> CanManageAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the viewer may see the tags on a person with this account and гурток — the one rule every
    /// read that names tags or their carriers goes by. Always false in a kurin with tags off.
    /// </summary>
    Task<Func<Guid?, Guid?, bool>> ShowsTagsOfAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// How far the viewer sees tags on others: the whole kurin for whoever manages them, otherwise the
    /// kurin's <c>SeeTags</c> row; nobody while tags are off. One's own tags are not counted here.
    /// </summary>
    Task<Common.Models.Authorization.KurinPolicy.CapabilityReach> CarrierReachAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Fills the tags the viewer may see on people seen in the kurin.</summary>
    Task FillAsync(Guid kurinKey, IReadOnlyCollection<MemberResponse> rows, CancellationToken cancellationToken = default);
}
