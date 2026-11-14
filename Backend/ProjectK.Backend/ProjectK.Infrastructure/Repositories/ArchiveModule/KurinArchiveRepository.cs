using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ArchiveModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.ArchiveModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ArchiveModule;

public class KurinArchiveRepository : IKurinArchiveRepository
{
    private readonly AppDbContext _context;

    public KurinArchiveRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Every гурток of the kurin, dissolved ones included.</summary>
    private IQueryable<Group> AllGroupsOf(Guid kurinKey) =>
        _context.Groups.IgnoreQueryFilters().Where(group => group.KurinKey == kurinKey);

    public async Task<IReadOnlyList<ArchivedOffice>> GetOfficesAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var groups = AllGroupsOf(kurinKey);
        return await (
                from history in _context.LeadershipHistories
                join leadership in _context.Leaderships on history.LeadershipKey equals leadership.LeadershipKey
                join groupRow in groups on leadership.GroupKey equals groupRow.GroupKey into inGroup
                from groupRow in inGroup.DefaultIfEmpty()
                where leadership.KurinKey == kurinKey || groupRow != null
                orderby history.StartDate descending
                select new ArchivedOffice(
                    history.LeadershipHistoryKey,
                    leadership.LeadershipKey,
                    leadership.Type,
                    history.Role,
                    history.MemberKey,
                    history.StartDate,
                    history.EndDate,
                    leadership.GroupKey,
                    groupRow == null ? null : groupRow.Name,
                    groupRow != null && groupRow.DissolvedAtUtc != null))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivedMembership>> GetFormerMembershipsAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var memberships = await _context.Memberships
            .Where(membership => membership.KurinKey == kurinKey && membership.LeftAtUtc != null)
            .OrderByDescending(membership => membership.LeftAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            return [];
        }

        var keys = memberships.Select(membership => membership.MembershipKey).ToList();
        var stints = await (
                from stint in _context.MembershipGroupStints.Where(stint => keys.Contains(stint.MembershipKey))
                join groupRow in AllGroupsOf(kurinKey) on stint.GroupKey equals groupRow.GroupKey into inGroup
                from groupRow in inGroup.DefaultIfEmpty()
                select new { stint.MembershipKey, Stint = new ArchivedStint(stint.GroupKey, groupRow == null ? null : groupRow.Name, stint.FromUtc, stint.ToUtc) })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var byMembership = stints.ToLookup(row => row.MembershipKey, row => row.Stint);

        return memberships
            .Select(membership => new ArchivedMembership(
                membership.MembershipKey,
                membership.MemberKey,
                membership.UserKey,
                membership.Kind,
                membership.JoinedAtUtc,
                membership.LeftAtUtc!.Value,
                byMembership[membership.MembershipKey].OrderBy(stint => stint.FromUtc ?? DateTime.MinValue).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<DissolvedGroup>> GetDissolvedGroupsAsync(Guid kurinKey, CancellationToken cancellationToken = default) =>
        await AllGroupsOf(kurinKey)
            .Where(group => group.DissolvedAtUtc != null)
            .OrderByDescending(group => group.DissolvedAtUtc)
            .Select(group => new DissolvedGroup(group.GroupKey, group.Name, group.DissolvedAtUtc!.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<LeadershipRole, string>> GetRoleNamesAsync(Guid kurinKey, CancellationToken cancellationToken = default) =>
        KurinRoleNames.Read(await _context.Kurins
            .Where(kurin => kurin.KurinKey == kurinKey)
            .Select(kurin => kurin.RoleNames)
            .FirstOrDefaultAsync(cancellationToken));

    public async Task<ArchivedOffice?> FindOfficeAsync(Guid kurinKey, Guid historyKey, CancellationToken cancellationToken = default)
    {
        var history = await _context.LeadershipHistories
            .Include(found => found.Leadership)
            .FirstOrDefaultAsync(found => found.LeadershipHistoryKey == historyKey, cancellationToken);
        if (history is null)
        {
            return null;
        }

        var leadership = history.Leadership;
        var group = leadership.GroupKey is { } groupKey
            ? await _context.Groups.IgnoreQueryFilters().FirstOrDefaultAsync(found => found.GroupKey == groupKey, cancellationToken)
            : null;
        if ((leadership.KurinKey ?? group?.KurinKey) != kurinKey)
        {
            return null;
        }

        return new ArchivedOffice(
            history.LeadershipHistoryKey,
            leadership.LeadershipKey,
            leadership.Type,
            history.Role,
            history.MemberKey,
            history.StartDate,
            history.EndDate,
            leadership.GroupKey,
            group?.Name,
            group?.DissolvedAtUtc is not null);
    }

    public async Task<MembershipPeriod?> FindMembershipAsync(Guid kurinKey, Guid membershipKey, CancellationToken cancellationToken = default)
    {
        var membership = await _context.Memberships.FirstOrDefaultAsync(found => found.MembershipKey == membershipKey, cancellationToken);
        return membership is null || membership.KurinKey != kurinKey
            ? null
            : new MembershipPeriod(
                membership.MembershipKey,
                membership.MemberKey,
                membership.GroupKey,
                membership.Kind,
                membership.JoinedAtUtc,
                membership.LeftAtUtc);
    }

    public async Task<IReadOnlyList<Guid>> GetStintGroupKeysAsync(Guid membershipKey, CancellationToken cancellationToken = default) =>
        await _context.MembershipGroupStints
            .Where(stint => stint.MembershipKey == membershipKey)
            .Select(stint => stint.GroupKey)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<DissolvedGroup?> FindDissolvedGroupAsync(Guid kurinKey, Guid groupKey, CancellationToken cancellationToken = default)
    {
        var group = await FindDissolvedGroupRowAsync(groupKey, cancellationToken);
        return group is null || group.KurinKey != kurinKey
            ? null
            : new DissolvedGroup(group.GroupKey, group.Name, group.DissolvedAtUtc!.Value);
    }

    private Task<Group?> FindDissolvedGroupRowAsync(Guid groupKey, CancellationToken cancellationToken) =>
        _context.Groups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(group => group.GroupKey == groupKey && group.DissolvedAtUtc != null, cancellationToken);

    public async Task CorrectOfficeAsync(Guid historyKey, LeadershipRole role, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (await _context.LeadershipHistories.FindAsync([historyKey], cancellationToken) is not { } history)
        {
            return;
        }

        history.Role = role;
        history.StartDate = startDate;
        history.EndDate = endDate;
    }

    public async Task RemoveOfficeAsync(Guid historyKey, CancellationToken cancellationToken = default)
    {
        if (await _context.LeadershipHistories.FindAsync([historyKey], cancellationToken) is { } history)
        {
            _context.LeadershipHistories.Remove(history);
        }
    }

    public async Task<int> RemoveOfficesHeldAsync(
        Guid memberKey,
        Guid kurinKey,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var groupKeys = AllGroupsOf(kurinKey).Select(group => (Guid?)group.GroupKey);
        var held = await _context.LeadershipHistories
            .Include(history => history.Leadership)
            .Where(history => history.MemberKey == memberKey
                              && (history.Leadership.KurinKey == kurinKey || groupKeys.Contains(history.Leadership.GroupKey))
                              && history.StartDate <= to
                              && history.EndDate != null
                              && history.EndDate >= from)
            .ToListAsync(cancellationToken);

        _context.LeadershipHistories.RemoveRange(held);
        return held.Count;
    }

    public async Task<bool> HoldsOpenOfficeSinceAsync(Guid memberKey, Guid kurinKey, DateOnly before, CancellationToken cancellationToken = default)
    {
        var groupKeys = AllGroupsOf(kurinKey).Select(group => (Guid?)group.GroupKey);
        return await _context.LeadershipHistories
            .AnyAsync(history => history.MemberKey == memberKey
                                 && history.EndDate == null
                                 && history.StartDate <= before
                                 && (history.Leadership.KurinKey == kurinKey || groupKeys.Contains(history.Leadership.GroupKey)),
                cancellationToken);
    }

    public async Task<IReadOnlyList<(DateTime JoinedAtUtc, DateTime? LeftAtUtc)>> GetOtherPeriodsAsync(
        Guid memberKey,
        Guid kurinKey,
        Guid exceptMembershipKey,
        CancellationToken cancellationToken = default)
    {
        var periods = await _context.Memberships
            .Where(membership => membership.MemberKey == memberKey && membership.KurinKey == kurinKey && membership.MembershipKey != exceptMembershipKey)
            .Select(membership => new { membership.JoinedAtUtc, membership.LeftAtUtc })
            .ToListAsync(cancellationToken);
        return periods.Select(period => (period.JoinedAtUtc, period.LeftAtUtc)).ToList();
    }

    public async Task CorrectMembershipAsync(
        Guid membershipKey,
        MembershipKind kind,
        DateTime joinedAtUtc,
        DateTime leftAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Memberships.FindAsync([membershipKey], cancellationToken) is not { } membership)
        {
            return;
        }

        var joinedWas = membership.JoinedAtUtc;
        var leftWas = membership.LeftAtUtc;
        membership.Kind = kind;
        membership.JoinedAtUtc = joinedAtUtc;
        membership.LeftAtUtc = leftAtUtc;

        foreach (var stint in await _context.MembershipGroupStints.Where(stint => stint.MembershipKey == membershipKey).ToListAsync(cancellationToken))
        {
            if (stint.FromUtc == joinedWas)
            {
                stint.FromUtc = joinedAtUtc;
            }

            if (stint.ToUtc == leftWas)
            {
                stint.ToUtc = leftAtUtc;
            }
        }
    }

    public async Task RemoveMembershipAsync(Guid membershipKey, CancellationToken cancellationToken = default)
    {
        if (await _context.Memberships.FindAsync([membershipKey], cancellationToken) is { } membership)
        {
            _context.Memberships.Remove(membership);
        }
    }

    public async Task<int> RemoveDissolvedGroupAsync(Guid groupKey, CancellationToken cancellationToken = default)
    {
        if (await FindDissolvedGroupRowAsync(groupKey, cancellationToken) is not { } group)
        {
            return 0;
        }

        var leaderships = await _context.Leaderships
            .Include(leadership => leadership.LeadershipHistories)
            .Where(leadership => leadership.GroupKey == group.GroupKey)
            .ToListAsync(cancellationToken);
        var holdings = leaderships.Sum(leadership => leadership.LeadershipHistories.Count);
        _context.Leaderships.RemoveRange(leaderships);

        _context.MembershipGroupStints.RemoveRange(
            await _context.MembershipGroupStints.Where(stint => stint.GroupKey == group.GroupKey).ToListAsync(cancellationToken));

        // Membership.Group is NO ACTION; dissolving detached them, this is for any that slipped back.
        foreach (var membership in await _context.Memberships.Where(membership => membership.GroupKey == group.GroupKey).ToListAsync(cancellationToken))
        {
            membership.GroupKey = null;
        }

        _context.Groups.Remove(group);
        return holdings;
    }
}
