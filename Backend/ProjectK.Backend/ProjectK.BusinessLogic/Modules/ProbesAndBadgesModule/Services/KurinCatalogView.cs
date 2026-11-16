using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

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
