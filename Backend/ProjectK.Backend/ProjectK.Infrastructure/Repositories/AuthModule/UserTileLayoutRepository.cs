using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

public class UserTileLayoutRepository : BaseEntityRepository<UserTileLayout>, IUserTileLayoutRepository
{

    public UserTileLayoutRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.UserTileLayouts
            .AnyAsync(x => x.UserTileLayoutKey == entityKey, cancellationToken);
    }

    public override async Task<IEnumerable<UserTileLayout>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.UserTileLayouts.AsNoTracking().ToListAsync(cancellationToken);
    }

    public override async Task<UserTileLayout?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.UserTileLayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserTileLayoutKey == entityKey, cancellationToken);
    }

    public async Task<IEnumerable<UserTileLayout>> GetByUserAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        return await Context.UserTileLayouts
            .AsNoTracking()
            .Where(x => x.UserKey == userKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserTileLayout?> GetByBoardAsync(Guid userKey, string boardKey, CancellationToken cancellationToken = default)
    {
        return await Context.UserTileLayouts
            .FirstOrDefaultAsync(x => x.UserKey == userKey && x.BoardKey == boardKey, cancellationToken);
    }

}
