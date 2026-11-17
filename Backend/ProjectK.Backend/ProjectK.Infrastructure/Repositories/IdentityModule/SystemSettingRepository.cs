using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.IdentityModule;

public class SystemSettingRepository : BaseEntityRepository<SystemSetting>, ISystemSettingRepository
{

    public SystemSettingRepository(AppDbContext context) : base(context)
    {
    }

    public override Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("SystemSetting uses a string key, not a Guid.");
    }

    public override Task<SystemSetting?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("SystemSetting uses a string key, not a Guid.");
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken token = default)
    {
        return await Context.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, token);
    }

    public async Task<IEnumerable<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Context.SystemSettings.ToListAsync(cancellationToken);

}
