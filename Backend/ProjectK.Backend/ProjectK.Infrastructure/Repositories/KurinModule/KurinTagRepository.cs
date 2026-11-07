using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class KurinTagRepository : BaseEntityRepository<KurinTag>, IKurinTagRepository
{
    public KurinTagRepository(AppDbContext context) : base(context)
    {
    }

    public override Task<IEnumerable<KurinTag>> GetAllAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Use GetForKurinAsync instead.");

    public async Task<IReadOnlyList<KurinTag>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default) =>
        await Context.KurinTags
            .Where(t => t.KurinKey == kurinKey)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<KurinTag?> GetWithMembersAsync(Guid kurinTagKey, CancellationToken cancellationToken = default) =>
        Context.KurinTags
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.KurinTagKey == kurinTagKey, cancellationToken);

    public Task<bool> NameTakenAsync(Guid kurinKey, string name, Guid? exceptKurinTagKey, CancellationToken cancellationToken = default) =>
        Context.KurinTags.AnyAsync(
            t => t.KurinKey == kurinKey
                && t.Name == name
                && (exceptKurinTagKey == null || t.KurinTagKey != exceptKurinTagKey),
            cancellationToken);

    // Leaving a kurin keeps a person's tags for a return, but whoever is not in it now carries nothing
    // there: no tag count, roster or «кожному окремо» target may wait on them.
    private IQueryable<MemberTag> CurrentCarriers =>
        Context.MemberTags.Where(a => Context.Memberships.Any(m =>
            m.MemberKey == a.MemberKey && m.KurinKey == a.Tag!.KurinKey && m.LeftAtUtc == null));

    public async Task<IReadOnlyList<MemberTag>> GetAssignmentsAsync(Guid kurinKey, CancellationToken cancellationToken = default) =>
        await CurrentCarriers
            .Where(a => a.Tag!.KurinKey == kurinKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberTag>> GetAssignmentsAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default) =>
        await CurrentCarriers
            .Where(a => a.Tag!.KurinKey == kurinKey && memberKeys.Contains(a.MemberKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberTag>> GetTrackedForMemberAsync(Guid kurinKey, Guid memberKey, CancellationToken cancellationToken = default) =>
        await Context.MemberTags
            .Where(a => a.MemberKey == memberKey && a.Tag!.KurinKey == kurinKey)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountPerMemberAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default) =>
        await CurrentCarriers
            .Where(a => a.Tag!.KurinKey == kurinKey && memberKeys.Contains(a.MemberKey))
            .GroupBy(a => a.MemberKey)
            .Select(g => new { MemberKey = g.Key, Count = g.Count() })
            .ToDictionaryAsync(row => row.MemberKey, row => row.Count, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetMemberKeysAsync(Guid kurinTagKey, CancellationToken cancellationToken = default) =>
        await CurrentCarriers
            .Where(a => a.KurinTagKey == kurinTagKey)
            .Select(a => a.MemberKey)
            .ToListAsync(cancellationToken);

    public async Task HoldMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        // An application lock per person, owned by the transaction; taken in key order so two holders
        // of overlapping sets cannot wait on each other. Other providers (tests) have no such lock.
        if (!Context.Database.IsSqlServer())
        {
            return;
        }

        foreach (var memberKey in memberKeys.Distinct().Order())
        {
            var resource = $"member-tags:{memberKey}";
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                cancellationToken);
        }
    }

    public void AddAssignment(MemberTag assignment) => Context.MemberTags.Add(assignment);

    public void RemoveAssignment(MemberTag assignment) => Context.MemberTags.Remove(assignment);

    public Task<KurinTagSettings?> GetSettingsAsync(Guid kurinKey, CancellationToken cancellationToken = default) =>
        Context.KurinTagSettings.FirstOrDefaultAsync(s => s.KurinKey == kurinKey, cancellationToken);

    public void AddSettings(KurinTagSettings settings) => Context.KurinTagSettings.Add(settings);
}
