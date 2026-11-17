using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Contracts.IdentityModule;

public interface IUserTileLayoutRepository : IBaseEntityRepository<UserTileLayout>
{
    Task<IEnumerable<UserTileLayout>> GetByUserAsync(Guid userKey, CancellationToken cancellationToken = default);
    Task<UserTileLayout?> GetByBoardAsync(Guid userKey, string boardKey, CancellationToken cancellationToken = default);
}
