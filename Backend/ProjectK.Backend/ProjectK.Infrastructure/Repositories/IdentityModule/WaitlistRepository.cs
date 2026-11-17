using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.IdentityModule;

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
