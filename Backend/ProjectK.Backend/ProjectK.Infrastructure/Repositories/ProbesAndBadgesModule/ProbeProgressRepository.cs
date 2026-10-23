using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;

public class ProbeProgressRepository : BaseEntityRepository<ProbeProgress>, IProbeProgressRepository
{

    public ProbeProgressRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<ProbeProgress?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProgresses
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ProbeProgressKey == entityKey, cancellationToken);
    }

    public async Task<ProbeProgress?> GetByMemberAndProbeIdAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProgresses
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.MemberKey == memberKey && x.ProbeId == probeId,
                cancellationToken);
    }

    public Task<int> CountByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Context.ProbeProgresses.CountAsync(x => x.MemberKey == memberKey, cancellationToken);

    public async Task<IReadOnlyList<ProbeProgress>> GetByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProgresses
            .Where(x => x.KurinKey == kurinKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProbeProgress>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProgresses
            .Where(x => x.MemberKey == memberKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProbeProgress>> GetByMemberKeysAsync(IEnumerable<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        var keys = memberKeys as IReadOnlyCollection<Guid> ?? memberKeys.ToList();
        if (keys.Count == 0)
        {
            return Array.Empty<ProbeProgress>();
        }

        return await Context.ProbeProgresses
            .Where(x => keys.Contains(x.MemberKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override Task<IEnumerable<ProbeProgress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetByMemberKeyAsync instead.");
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProgresses
            .AnyAsync(x => x.ProbeProgressKey == entityKey, cancellationToken);
    }

    public override void Update(ProbeProgress entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return;
        }

        // Through the tracker rather than ExecuteDelete: this runs inside a use case whose
        // SaveChanges has not happened yet, and an out-of-band delete would commit ahead of it.
        var rows = await Context.ProbeProgresses
            .Where(row => memberKeys.Contains(row.MemberKey))
            .ToListAsync(cancellationToken);

        Context.ProbeProgresses.RemoveRange(rows);
    }
}
