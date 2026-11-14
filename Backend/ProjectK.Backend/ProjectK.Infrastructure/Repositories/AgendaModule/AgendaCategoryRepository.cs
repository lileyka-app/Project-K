using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AgendaModule;

public class AgendaCategoryRepository : BaseEntityRepository<AgendaCategory>, IAgendaCategoryRepository
{

    public AgendaCategoryRepository(AppDbContext context) : base(context)
    {
    }

    public override Task<IEnumerable<AgendaCategory>> GetAllAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Use GetForKurinAsync instead.");

    public async Task<IReadOnlyList<AgendaCategory>> GetForKurinAsync(Guid kurinKey, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = Context.AgendaCategories.Where(c => c.KurinKey == kurinKey);
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
