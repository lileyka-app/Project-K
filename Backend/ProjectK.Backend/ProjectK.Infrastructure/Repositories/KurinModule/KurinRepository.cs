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

public class KurinRepository : BaseEntityRepository<Kurin>, IKurinRepository
{
    public KurinRepository(AppDbContext context) : base(context)
    {
    }

    public override void Create(Kurin kurin, CancellationToken token = default)
    {
        Context.Kurins.Add(kurin);
    }

    public override void Delete(Kurin kurin, CancellationToken token = default)
    {
        Context.Kurins.Remove(kurin);
    }

    public override async Task<Kurin?> GetByKeyAsync(Guid entityKey, CancellationToken token = default)
    {
        return await Context.Kurins.FirstOrDefaultAsync(k => k.KurinKey == entityKey, token);
    }

    public async Task<Kurin?> GetByNumberAsync(string kraiCode, int number, CancellationToken token = default)
    {
        return await Context.Kurins.FirstOrDefaultAsync(k => k.KraiCode == kraiCode && k.Number == number, token);
    }

    public override async Task<IEnumerable<Kurin>> GetAllAsync(CancellationToken token = default)
    {
        return await Context.Kurins.ToListAsync(token);
    }

    public async Task<bool> ExistsAsync(string kraiCode, int number, CancellationToken token = default)
    {
        return await Context.Kurins.AnyAsync(k => k.KraiCode == kraiCode && k.Number == number, token);
    }

    public async Task<IReadOnlyList<Kurin>> GetInKraiAsync(string kraiCode, CancellationToken token = default)
    {
        return await Context.Kurins
            .AsNoTracking()
            .Where(k => k.KraiCode == kraiCode)
            .OrderBy(k => k.Number == null)
            .ThenBy(k => k.Number)
            .ToListAsync(token);
    }

    public override void Update(Kurin kurin, CancellationToken token = default)
    {
        Context.Kurins.Update(kurin);
    }
}
