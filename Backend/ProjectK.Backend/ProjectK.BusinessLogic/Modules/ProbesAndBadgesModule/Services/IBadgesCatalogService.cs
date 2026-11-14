using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public interface IBadgesCatalogService
{
    BadgesMetadata GetBadgesMetadata();

    IReadOnlyList<Badge> GetBadges(int take);

    Badge? GetBadgeById(string id);
}
