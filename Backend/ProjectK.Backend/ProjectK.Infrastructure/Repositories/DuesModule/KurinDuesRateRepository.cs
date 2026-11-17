using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class KurinDuesRateRepository : BaseEntityRepository<KurinDuesRate>, IKurinDuesRateRepository
{
    public KurinDuesRateRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<KurinDuesRate>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.FromQuarter)
            .ToListAsync(cancellationToken);
}
