using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
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

    public override Task<IEnumerable<Group>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetAllAsync(Guid kurinKey) instead.");
    }

    public override void Update(Group group, CancellationToken cancellationToken = default)
    {
        Context.Groups.Update(group);
    }
}
