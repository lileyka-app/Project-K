using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class DuesConcessionGrantRepository : BaseEntityRepository<DuesConcessionGrant>, IDuesConcessionGrantRepository
{
    public DuesConcessionGrantRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DuesConcessionGrant>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.FromQuarter).ThenBy(e => e.CreatedDate)
            .ToListAsync(cancellationToken);

    public Task<DuesConcessionGrant?> GetForUpdateAsync(Guid grantKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.DuesConcessionGrantKey == grantKey, cancellationToken);

    public async Task DeleteForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default)
    {
        if (membershipKeys.Count == 0)
        {
            return;
        }

        Set.RemoveRange(await Set.Where(c => membershipKeys.Contains(c.MembershipKey)).ToListAsync(cancellationToken));
    }
}
