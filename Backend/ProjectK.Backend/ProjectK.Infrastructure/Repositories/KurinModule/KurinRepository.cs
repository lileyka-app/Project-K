using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class KurinRepository : BaseEntityRepository<Kurin>, IKurinRepository
{
    public KurinRepository(AppDbContext context) : base(context)
    {
    }

    public override void Create(Kurin kurin, CancellationToken cancellationToken = default)
    {
        Context.Kurins.Add(kurin);
    }

    public override void Delete(Kurin kurin, CancellationToken cancellationToken = default)
    {
        Context.Kurins.Remove(kurin);
    }

    public override async Task<Kurin?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.Kurins.FirstOrDefaultAsync(k => k.KurinKey == entityKey, cancellationToken);
    }

    public async Task<Kurin?> GetByNumberAsync(string kraiCode, int number, CancellationToken token = default, KurinBranch branch = KurinBranch.UPYu)
    {
        return await Context.Kurins.FirstOrDefaultAsync(
            k => k.KraiCode == kraiCode && k.Branches.Any(b => b.Branch == branch && b.Number == number),
            token);
    }

    public override async Task<IEnumerable<Kurin>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Kurins.ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(string kraiCode, int number, CancellationToken token = default)
        => IsNumberTakenAsync(kraiCode, KurinBranch.UPYu, number, null, token);

    public async Task<bool> IsNumberTakenAsync(string kraiCode, KurinBranch branch, int number, Guid? exceptKurinKey = null, CancellationToken token = default)
    {
        return await Context.Kurins.AnyAsync(
            k => k.KraiCode == kraiCode
                 && k.KurinKey != exceptKurinKey
                 && k.Branches.Any(b => b.Branch == branch && b.Number == number),
            token);
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

    public override void Update(Kurin kurin, CancellationToken cancellationToken = default)
    {
        Context.Kurins.Update(kurin);
    }
}
