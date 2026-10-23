using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;

public class BadgeProgressRepository : BaseEntityRepository<BadgeProgress>, IBadgeProgressRepository
{

    public BadgeProgressRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<BadgeProgress?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.BadgeProgresses
            .AsTracking()
            .FirstOrDefaultAsync(x => x.BadgeProgressKey == entityKey, cancellationToken);
    }

    public async Task<BadgeProgress?> GetByMemberAndBadgeIdAsync(Guid memberKey, string badgeId, CancellationToken cancellationToken = default)
    {
        return await Context.BadgeProgresses
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.MemberKey == memberKey && x.BadgeId == badgeId,
                cancellationToken);
    }

    public Task<int> CountByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Context.BadgeProgresses.CountAsync(x => x.MemberKey == memberKey, cancellationToken);

    public async Task<IReadOnlyList<BadgeProgress>> GetByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await Context.BadgeProgresses
            .Where(x => x.KurinKey == kurinKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BadgeProgress>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await Context.BadgeProgresses
            .Where(x => x.MemberKey == memberKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BadgeProgress>> GetByMemberKeysAsync(IEnumerable<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        var keys = memberKeys as IReadOnlyCollection<Guid> ?? memberKeys.ToList();
        if (keys.Count == 0)
        {
            return Array.Empty<BadgeProgress>();
        }

        return await Context.BadgeProgresses
            .Where(x => keys.Contains(x.MemberKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override Task<IEnumerable<BadgeProgress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetByMemberKeyAsync instead.");
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.BadgeProgresses
            .AnyAsync(x => x.BadgeProgressKey == entityKey, cancellationToken);
    }

    public override void Update(BadgeProgress entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return;
        }

        // Through the tracker rather than ExecuteDelete: this runs inside a use case whose
        // SaveChanges has not happened yet, and an out-of-band delete would commit ahead of it.
        var rows = await Context.BadgeProgresses
            .Where(row => memberKeys.Contains(row.MemberKey))
            .ToListAsync(cancellationToken);

        Context.BadgeProgresses.RemoveRange(rows);
    }
}
