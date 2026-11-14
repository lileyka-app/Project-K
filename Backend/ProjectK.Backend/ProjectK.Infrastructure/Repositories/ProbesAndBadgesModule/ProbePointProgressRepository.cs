using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;

public class ProbePointProgressRepository : BaseEntityRepository<ProbePointProgress>, IProbePointProgressRepository
{

    public ProbePointProgressRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<ProbePointProgress?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbePointProgresses
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ProbePointProgressKey == entityKey, cancellationToken);
    }

    public async Task<ProbePointProgress?> GetByMemberProbePointAsync(
        Guid memberKey,
        string probeId,
        string pointId,
        CancellationToken cancellationToken = default)
    {
        return await Context.ProbePointProgresses
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.MemberKey == memberKey && x.ProbeId == probeId && x.PointId == pointId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ProbePointProgress>> GetSignedByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbePointProgresses
            .Where(x => x.KurinKey == kurinKey && x.IsSigned)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProbePointProgress>> GetByMemberKeysAsync(IEnumerable<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        var keys = memberKeys as IReadOnlyCollection<Guid> ?? memberKeys.ToList();
        if (keys.Count == 0)
        {
            return Array.Empty<ProbePointProgress>();
        }

        return await Context.ProbePointProgresses
            .Where(x => keys.Contains(x.MemberKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProbePointProgress>> GetByMemberAndProbeAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken = default)
    {
        return await Context.ProbePointProgresses
            .Where(x => x.MemberKey == memberKey && x.ProbeId == probeId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override Task<IEnumerable<ProbePointProgress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetByMemberAndProbeAsync instead.");
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbePointProgresses
            .AnyAsync(x => x.ProbePointProgressKey == entityKey, cancellationToken);
    }

    public override void Update(ProbePointProgress entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return;
        }

        // Through the tracker rather than ExecuteDelete: this runs inside a use case whose
        // SaveChanges has not happened yet, and an out-of-band delete would commit ahead of it.
        var rows = await Context.ProbePointProgresses
            .Where(row => memberKeys.Contains(row.MemberKey))
            .ToListAsync(cancellationToken);

        Context.ProbePointProgresses.RemoveRange(rows);
    }
}
