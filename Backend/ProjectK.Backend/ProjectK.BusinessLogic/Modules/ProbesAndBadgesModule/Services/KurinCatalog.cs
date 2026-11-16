using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public sealed class KurinCatalog : IKurinCatalog
{
    private readonly IKraiProfileDirectory _profiles;
    private readonly IBadgesCatalog _badges;

    public KurinCatalog(IKraiProfileDirectory profiles, IBadgesCatalog badges)
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
