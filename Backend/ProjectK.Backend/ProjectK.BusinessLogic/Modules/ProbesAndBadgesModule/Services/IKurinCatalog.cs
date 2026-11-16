namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// The вмілості a kurin is offered: the catalogue's entries from the source краї of its край's
/// profile, less the ones the profile hides. Only what is offered for a new start; whatever a member
/// already has is still read from the whole catalogue, so nothing started disappears. Проби are not
/// filtered here: which edition of them a person walks is <see cref="ProbeLineRules"/>'s to say.
/// </summary>
public interface IKurinCatalog
{
    /// <summary>Null when the kurin does not exist.</summary>
    Task<KurinCatalogView?> ForAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
