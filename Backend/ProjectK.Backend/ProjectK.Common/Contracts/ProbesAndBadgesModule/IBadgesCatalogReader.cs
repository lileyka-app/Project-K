using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

public interface IBadgesCatalogReader
{
    BadgesMetadata GetBadgesMetadata();

    IReadOnlyList<Badge> GetBadges(int take);

    Badge? GetBadgeById(string id);
}
