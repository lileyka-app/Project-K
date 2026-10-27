using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// The вмілості a kurin is offered: the catalogue's entries from the source краї of its край's
/// profile, less the ones the profile hides. Only what is offered for a new start; whatever a member
/// already has is still read from the whole catalogue, so nothing started disappears. Проби are not
/// filtered here: which edition of them a person walks is <see cref="ProbeLine"/>'s to say.
/// </summary>
public interface IKurinCatalog
{
    /// <summary>Null when the kurin does not exist.</summary>
    Task<KurinCatalogView?> ForAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}

public sealed class KurinCatalogView
{
    private readonly HashSet<string> _badgeIds;

    public KurinCatalogView(IReadOnlyList<Badge> badges)
    {
        Badges = badges;
        _badgeIds = badges.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<Badge> Badges { get; }

    public bool OffersBadge(string badgeId) => _badgeIds.Contains(badgeId);
}

public sealed class KurinCatalog : IKurinCatalog
{
    private readonly IKraiProfileProvider _profiles;
    private readonly IBadgesCatalog _badges;

    public KurinCatalog(IKraiProfileProvider profiles, IBadgesCatalog badges)
    {
        _profiles = profiles;
        _badges = badges;
    }

    public async Task<KurinCatalogView?> ForAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetForKurinAsync(kurinKey, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var catalog = profile.Catalog;
        var badgeSources = catalog.BadgeSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hiddenBadges = catalog.HiddenBadges.ToHashSet(StringComparer.Ordinal);

        // A вмілість the catalogue gives no край belongs to no source, so no kurin is offered it.
        return new KurinCatalogView(
            _badges.GetAll().Where(b => badgeSources.Contains(b.KraiCode) && !hiddenBadges.Contains(b.Id)).ToList());
    }
}
