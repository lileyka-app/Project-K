using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class GroupRepository : BaseEntityRepository<Group>, IGroupRepository
{
    public GroupRepository(AppDbContext context) : base(context)
    {
    }

    public override void Create(Group group, CancellationToken cancellationToken = default)
    {
        Context.Groups.Add(group);
    }

    public override void Delete(Group group, CancellationToken cancellationToken = default)
    {
        Context.Groups.Remove(group);
    }

    public override async Task<Group?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.Groups.Include(g => g.Kurin).FirstOrDefaultAsync(e => e.GroupKey == entityKey, cancellationToken);
    }

    public async Task<IEnumerable<Group>> GetAllAsync(Guid kurinKey, CancellationToken token = default)
    {
        return await Context.Groups.Where(g => g.KurinKey == kurinKey).Include(g => g.Kurin).AsNoTracking().ToListAsync(token);
    }

    public async Task<IReadOnlyList<Group>> GetByKeysIncludingDissolvedAsync(IReadOnlyCollection<Guid> groupKeys, CancellationToken cancellationToken = default)
    {
        if (groupKeys.Count == 0)
        {
            return [];
        }

        return await Context.Groups.IgnoreQueryFilters()
            .Where(g => groupKeys.Contains(g.GroupKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override void Update(Group group, CancellationToken cancellationToken = default)
    {
        Context.Groups.Update(group);
    }
}
