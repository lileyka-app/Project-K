using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

public class WaitlistRepository : BaseEntityRepository<WaitlistEntry>, IWaitlistRepository
{

    public WaitlistRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await Context.WaitlistEntries.FirstOrDefaultAsync(e => e.Email == email, cancellationToken);
    }

    public async Task<IEnumerable<WaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Context.WaitlistEntries.ToListAsync(cancellationToken);

}
