using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class DuesChargeRepository : BaseEntityRepository<DuesCharge>, IDuesChargeRepository
{
    public DuesChargeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DuesCharge>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.Quarter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DuesCharge>> GetForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default)
        => membershipKeys.Count == 0
            ? []
            : await Set
                .AsNoTracking()
                .Where(c => membershipKeys.Contains(c.MembershipKey))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DuesCharge>> GetTrackedFromQuarterAsync(Guid kurinKey, int fromQuarter, CancellationToken cancellationToken = default)
        => await Set
            .Where(c => c.KurinKey == kurinKey && c.Quarter >= fromQuarter)
            .ToListAsync(cancellationToken);

    public async Task DeleteForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default)
    {
        if (membershipKeys.Count == 0)
        {
            return;
        }

        Set.RemoveRange(await Set.Where(c => membershipKeys.Contains(c.MembershipKey)).ToListAsync(cancellationToken));
    }

    public async Task DeleteFromQuarterAsync(Guid membershipKey, int fromQuarter, CancellationToken cancellationToken = default)
        => Set.RemoveRange(await Set
            .Where(c => c.MembershipKey == membershipKey && c.Quarter >= fromQuarter)
            .ToListAsync(cancellationToken));
}
