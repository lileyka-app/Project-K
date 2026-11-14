using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MemberWarningRepository : BaseEntityRepository<MemberWarning>, IMemberWarningRepository
{

    public MemberWarningRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<MemberWarning?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .AsTracking()
            .FirstOrDefaultAsync(x => x.MemberWarningKey == entityKey, cancellationToken);
    }

    public override async Task<IEnumerable<MemberWarning>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .AnyAsync(x => x.MemberWarningKey == entityKey, cancellationToken);
    }

    public override void Update(MemberWarning entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task<IReadOnlyCollection<MemberWarning>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .Where(x => x.MemberKey == memberKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MemberWarning>> GetActiveByKurinKeyAsync(Guid kurinKey, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .Where(x => x.KurinKey == kurinKey && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MemberWarning>> GetActiveByMemberKeyAsync(Guid memberKey, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .Where(x => x.MemberKey == memberKey && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .AsTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberWarning?> GetActiveByMemberAndLevelAsync(Guid memberKey, MemberWarningLevel level, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .Where(x => x.MemberKey == memberKey && x.Level == level && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> ExpireActiveWarningsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await Context.MemberWarnings
            .Where(w => w.RevokedAtUtc == null && w.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.RevokedAtUtc, nowUtc)
                .SetProperty(w => w.UpdatedDate, nowUtc),
                cancellationToken);
    }
}
