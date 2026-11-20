using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class LeadershipRepository : ILeadershipRepository
{
    private readonly AppDbContext _context;
    public LeadershipRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Leadership?> GetByKeyAsync(Guid leadershipKey, CancellationToken cancellationToken = default)
    {
        return await _context.Leaderships
                             .Include(l => l.LeadershipHistories)
                             .FirstOrDefaultAsync(l => l.LeadershipKey == leadershipKey, cancellationToken);
    }

    public async Task<IEnumerable<Leadership>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Leaderships
                             .Include(l => l.LeadershipHistories)
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Leadership>> GetAllByTypeAsync(LeadershipType type, Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await _context.Leaderships
                             .Where(l =>
                                l.Type == type &&
                                (
                                    (type == LeadershipType.Kurin || type == LeadershipType.KV)
                                    && l.KurinKey == entityKey
                                )
                                ||
                                (
                                    type == LeadershipType.Group
                                    && l.GroupKey == entityKey
                                )
                             )
                             .Include(l => l.LeadershipHistories)
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }

    public void Add(Leadership leadership, CancellationToken cancellationToken = default)
    {
        _context.Leaderships.AddAsync(leadership, cancellationToken);
    }

    public void Update(Leadership leadership, CancellationToken cancellationToken = default)
    {
        _context.Leaderships.Update(leadership);
    }

    /// <summary>
    /// Loaded tracked and without its history, unlike <see cref="GetAllByTypeAsync"/>: that one
    /// reads <c>AsNoTracking</c> with the members attached, and re-attaching that graph for a
    /// delete would put a second copy of an already-tracked member in front of EF.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> DeleteForGroupAsync(Guid groupKey, CancellationToken cancellationToken = default)
    {
        var leaderships = await _context.Leaderships
            .Where(leadership => leadership.GroupKey == groupKey)
            .ToListAsync(cancellationToken);

        _context.Leaderships.RemoveRange(leaderships);

        return leaderships.Select(leadership => leadership.LeadershipKey).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> CloseForGroupAsync(Guid groupKey, DateOnly on, CancellationToken cancellationToken = default)
    {
        var leaderships = await _context.Leaderships
            .Where(leadership => leadership.GroupKey == groupKey)
            .Include(leadership => leadership.LeadershipHistories.Where(history => history.EndDate == null))
            .ToListAsync(cancellationToken);

        foreach (var leadership in leaderships)
        {
            leadership.EndDate ??= on;
            foreach (var history in leadership.LeadershipHistories)
            {
                history.EndDate = on;
            }
        }

        return leaderships.Select(leadership => leadership.LeadershipKey).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> DeleteForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        // Dissolved гуртки too: their провід is still there and would refuse the kurin's delete.
        var groupKeys = await _context.Groups
            .IgnoreQueryFilters()
            .Where(group => group.KurinKey == kurinKey)
            .Select(group => group.GroupKey)
            .ToListAsync(cancellationToken);

        var leaderships = await _context.Leaderships
            .Where(leadership => leadership.KurinKey == kurinKey
                || (leadership.GroupKey != null && groupKeys.Contains(leadership.GroupKey.Value)))
            .ToListAsync(cancellationToken);

        _context.Leaderships.RemoveRange(leaderships);

        return leaderships.Select(leadership => leadership.LeadershipKey).ToList();
    }

    public async Task<IEnumerable<LeadershipHistory>> GetLeadershipHistoriesAsync(Guid leadershipKey, CancellationToken cancellationToken = default)
    {
        return await _context.LeadershipHistories
                             .Where(h => h.LeadershipKey == leadershipKey)
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeadershipHistory>> GetHistoriesForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => await _context.LeadershipHistories
            .AsNoTracking()
            .Include(h => h.Leadership)
            .Where(h => h.MemberKey == memberKey)
            .ToListAsync(cancellationToken);

    public void LeadershipHistoriesRemoveRange(IEnumerable<LeadershipHistory> histories)
    {
        _context.LeadershipHistories.RemoveRange(histories);
    }

    public async Task<IReadOnlyList<MemberOffice>> GetActiveOfficesForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await _context.LeadershipHistories
                             .Where(h => h.MemberKey == memberKey && h.EndDate == null)
                             .Join(_context.Leaderships,
                                   h => h.LeadershipKey,
                                   l => l.LeadershipKey,
                                   (h, l) => new MemberOffice(l.Type, h.Role))
                             .Distinct()
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberOffice>> GetActiveOfficesForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
    {
        var belongsHere = _context.Memberships
            .Where(ms => ms.UserKey == userKey && ms.KurinKey == kurinKey && ms.LeftAtUtc == null);

        // An office belongs to a провід, and a провід to this kurin either directly or through
        // one of its гуртки. Both have to be allowed for, or every гуртковий office falls out.
        var officesOfThisKurin = _context.Leaderships
            .Where(l => l.EndDate == null
                && (l.KurinKey == kurinKey
                    || _context.Groups.Any(g => g.GroupKey == l.GroupKey && g.KurinKey == kurinKey)));

        return await (from ms in belongsHere
                      join history in _context.LeadershipHistories.Where(h => h.EndDate == null)
                          on ms.MemberKey equals history.MemberKey
                      join office in officesOfThisKurin
                          on history.LeadershipKey equals office.LeadershipKey
                      select new MemberOffice(office.Type, history.Role))
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveOfficeMemberKeysAsync(
        IReadOnlyCollection<LeadershipRole> roles,
        Guid? kurinKey = null,
        Guid? groupKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeadershipHistories
                            .Where(h => h.EndDate == null && roles.Contains(h.Role))
                            .Join(_context.Leaderships,
                                  h => h.LeadershipKey,
                                  l => l.LeadershipKey,
                                  (h, l) => new { h.MemberKey, l.KurinKey, l.GroupKey });

        if (kurinKey.HasValue)
        {
            query = query.Where(x => x.KurinKey == kurinKey.Value);
        }

        if (groupKey.HasValue)
        {
            query = query.Where(x => x.GroupKey == groupKey.Value);
        }

        return await query
                     .Select(x => x.MemberKey)
                     .Distinct()
                     .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberLookupRow>> GetOfficeHoldersAsync(
        Guid kurinKey,
        LeadershipType type,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.LeadershipHistories
                                 .Where(h => h.EndDate == null)
                                 .Join(_context.Leaderships.Where(l => l.Type == type && l.KurinKey == kurinKey),
                                       h => h.LeadershipKey,
                                       l => l.LeadershipKey,
                                       (h, l) => h)
                                 .Join(_context.Members,
                                       h => h.MemberKey,
                                       m => m.MemberKey,
                                       (h, m) => new { m.MemberKey, m.UserKey, m.FirstName, m.MiddleName, m.LastName, m.ProfilePhotoBlobName, h.Role })
                                 .AsNoTracking()
                                 .ToListAsync(cancellationToken);

        return rows
            .Select(r => new MemberLookupRow(
                r.MemberKey,
                r.UserKey,
                r.FirstName,
                r.MiddleName,
                r.LastName,
                r.ProfilePhotoBlobName,
                new MemberOffice(type, r.Role)))
            .ToList();
    }

    public async Task<IReadOnlyList<LeadershipRef>> GetLeadershipRefsForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var groupKeys = await _context.Groups
            .Where(g => g.KurinKey == kurinKey)
            .Select(g => g.GroupKey)
            .ToListAsync(cancellationToken);

        return await _context.Leaderships
            .Where(l => l.KurinKey == kurinKey || (l.GroupKey != null && groupKeys.Contains(l.GroupKey.Value)))
            .Select(l => new LeadershipRef(l.LeadershipKey, l.Type, l.GroupKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberUserKeysForLeadershipAsync(Guid leadershipKey, CancellationToken cancellationToken = default)
    {
        return await _context.LeadershipHistories
            .Where(h => h.LeadershipKey == leadershipKey && h.EndDate == null)
            .Join(_context.Members, h => h.MemberKey, m => m.MemberKey, (h, m) => m.UserKey)
            .Where(userKey => userKey != null)
            .Select(userKey => userKey!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberKeysForLeadershipAsync(Guid leadershipKey, CancellationToken cancellationToken = default)
    {
        return await _context.LeadershipHistories
            .Where(h => h.LeadershipKey == leadershipKey && h.EndDate == null)
            .Select(h => h.MemberKey)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveLeadershipKeysForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await _context.LeadershipHistories
            .Where(h => h.MemberKey == memberKey && h.EndDate == null)
            .Select(h => h.LeadershipKey)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
