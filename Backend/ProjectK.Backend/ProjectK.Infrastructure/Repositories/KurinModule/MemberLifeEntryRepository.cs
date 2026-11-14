using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MemberLifeEntryRepository : BaseEntityRepository<MemberLifeEntry>, IMemberLifeEntryRepository
{
    public MemberLifeEntryRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<MemberLifeEntry>> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.MemberKey == memberKey)
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.CreatedDate)
            .ToListAsync(cancellationToken);

    public Task<MemberLifeEntry?> GetOwnAsync(Guid memberKey, Guid entryKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.MemberKey == memberKey && e.MemberLifeEntryKey == entryKey, cancellationToken);
}
