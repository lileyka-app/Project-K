using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class DuesEntryRepository : BaseEntityRepository<DuesEntry>, IDuesEntryRepository
{
    public DuesEntryRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>Tracked, with its trail, so a change and its event are written together.</summary>
    public override Task<DuesEntry?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
        => Set
            .Include(e => e.Events)
            .FirstOrDefaultAsync(e => e.DuesEntryKey == entityKey, cancellationToken);

    public async Task<IReadOnlyList<DuesEntry>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey && e.DeletedAtUtc == null)
            .OrderBy(e => e.OccurredOn)
            .ThenBy(e => e.CreatedDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DuesEntry>> GetForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default)
        => membershipKeys.Count == 0
            ? []
            : await Set
                .Include(e => e.Events)
                .Where(e => e.MembershipKey != null && membershipKeys.Contains(e.MembershipKey.Value) && e.DeletedAtUtc == null)
                .ToListAsync(cancellationToken);

    /// <summary>Money is never removed: deleting is a mark, made by the handler.</summary>
    public override void Delete(DuesEntry entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Dues entries are marked deleted, never removed.");
}
