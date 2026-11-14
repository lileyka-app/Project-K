using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

public interface IUserTileLayoutRepository : IBaseEntityRepository<UserTileLayout>
{
    Task<IEnumerable<UserTileLayout>> GetByUserAsync(Guid userKey, CancellationToken cancellationToken = default);
    Task<UserTileLayout?> GetByBoardAsync(Guid userKey, string boardKey, CancellationToken cancellationToken = default);
}
