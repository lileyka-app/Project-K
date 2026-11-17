using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class GroupDuesRateRepository : BaseEntityRepository<GroupDuesRate>, IGroupDuesRateRepository
{
    public GroupDuesRateRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<GroupDuesRate>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.FromQuarter)
            .ToListAsync(cancellationToken);
}
