using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.InfrastructureModule;

public class ResourceScopeReader : IResourceScopeReader
{
    private readonly AppDbContext _context;

    public ResourceScopeReader(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResourceScope?> GetScopeAsync(
        ResourceType resourceType,
        Guid resourceKey,
        Guid inKurinKey,
        CancellationToken cancellationToken = default)
    {
        return resourceType switch
        {
            // A person is in scope through their membership, not through their record. That is
            // what keeps the access decision independent of the member module — and what makes
            // a Виховник in one kurin an ordinary member in another.
            ResourceType.Member => await MembershipScopeAsync(resourceKey, inKurinKey, cancellationToken),

            // A person's data is keyed by the person and reaches as far as they do.
            ResourceType.MemberContacts or ResourceType.MemberBirthDate or ResourceType.MemberBirthday
                or ResourceType.MemberAddress or ResourceType.MemberParents or ResourceType.MemberExtras
                => await MembershipScopeAsync(resourceKey, inKurinKey, cancellationToken),

            ResourceType.KurinJournal => await GetJournalScopeAsync(resourceKey, cancellationToken),

            ResourceType.Group => await _context.Groups
                .Where(g => g.GroupKey == resourceKey)
                .Select(g => new ResourceScope(g.KurinKey, g.GroupKey, null))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.Kurin => await _context.Kurins
                .Where(k => k.KurinKey == resourceKey)
                .Select(k => new ResourceScope(k.KurinKey, null, null))
                .FirstOrDefaultAsync(cancellationToken),

            // A box is addressed by what owns it: a гурток's by the гурток, the kurin's by the kurin.
            ResourceType.GroupDues => await _context.Groups
                .Where(g => g.GroupKey == resourceKey)
                .Select(g => new ResourceScope(g.KurinKey, g.GroupKey, null))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.GroupScore or ResourceType.GroupProbeEdition => await _context.Groups
                .Where(g => g.GroupKey == resourceKey)
                .Select(g => new ResourceScope(g.KurinKey, g.GroupKey, null))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.KurinScore or ResourceType.KurinScorePrivate => await _context.Kurins
                .Where(k => k.KurinKey == resourceKey)
                .Select(k => new ResourceScope(k.KurinKey, null, null))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.KurinDues => await _context.Kurins
                .Where(k => k.KurinKey == resourceKey)
                .Select(k => new ResourceScope(k.KurinKey, null, null))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.PlanningSession => await _context.PlanningSessions
                .Where(p => p.PlanningSessionKey == resourceKey)
                .Select(p => new ResourceScope(p.KurinKey, null, p.CreatedByUserKey))
                .FirstOrDefaultAsync(cancellationToken),

            ResourceType.Leadership => await GetLeadershipScopeAsync(resourceKey, cancellationToken),

            ResourceType.AgendaItem => await GetAgendaItemScopeAsync(resourceKey, cancellationToken),

            // Progress belongs to a person; the rules about it are the rules about them, so it
            // resolves through the same membership.
            ResourceType.ProbeProgress => await ProgressScopeAsync(
                _context.ProbeProgresses
                    .Where(p => p.ProbeProgressKey == resourceKey)
                    .Select(p => p.MemberKey),
                inKurinKey,
                cancellationToken),

            ResourceType.BadgeProgress => await ProgressScopeAsync(
                _context.BadgeProgresses
                    .Where(b => b.BadgeProgressKey == resourceKey)
                    .Select(b => b.MemberKey),
                inKurinKey,
                cancellationToken),

            _ => null
        };
    }

    private Task<ResourceScope?> MembershipScopeAsync(
        Guid memberKey,
        Guid inKurinKey,
        CancellationToken cancellationToken)
        => _context.Memberships
            .Where(ms => ms.MemberKey == memberKey
                         && ms.KurinKey == inKurinKey
                         && ms.LeftAtUtc == null)
            .Select(ms => new ResourceScope(ms.KurinKey, ms.GroupKey, ms.UserKey))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ResourceScope?> ProgressScopeAsync(
        IQueryable<Guid> memberKeys,
        Guid inKurinKey,
        CancellationToken cancellationToken)
    {
        var memberKey = await memberKeys.FirstOrDefaultAsync(cancellationToken);
        return memberKey == Guid.Empty
            ? null
            : await MembershipScopeAsync(memberKey, inKurinKey, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetLedGroupKeysAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
    {
        // Primary source: groups where the user currently holds a гуртковий-провід office.
        var officeGroups = await _context.LeadershipHistories
            .Where(h => h.EndDate == null)
            .Join(
                _context.Leaderships.Where(l => l.Type == LeadershipType.Group && l.GroupKey != null),
                h => h.LeadershipKey,
                l => l.LeadershipKey,
                (h, l) => new { h.MemberKey, GroupKey = l.GroupKey!.Value })
            .Join(
                _context.Memberships.Where(ms => ms.UserKey == userKey
                                                 && ms.KurinKey == kurinKey
                                                 && ms.LeftAtUtc == null),
                x => x.MemberKey,
                ms => ms.MemberKey,
                (x, ms) => x.GroupKey)
            .ToListAsync(cancellationToken);

        // Legacy source: explicit mentor assignments, kept until fully migrated to offices.
        var assigned = await _context.MentorAssignments
            .Where(a => a.MentorUserKey == userKey && a.RevokedAtUtc == null)
            .Select(a => a.GroupKey)
            .ToListAsync(cancellationToken);

        // Compatibility fallback: a group leader also covers the group they are a member of.
        var ownGroupKey = await _context.Memberships
            .Where(ms => ms.KurinKey == kurinKey
                         && ms.UserKey == userKey
                         && ms.LeftAtUtc == null
                         && ms.GroupKey != null)
            .Select(ms => ms.GroupKey)
            .FirstOrDefaultAsync(cancellationToken);

        var ledGroups = officeGroups.Concat(assigned);
        if (ownGroupKey.HasValue)
        {
            ledGroups = ledGroups.Append(ownGroupKey.Value);
        }

        return ledGroups.Distinct().ToArray();
    }

    /// <summary>
    /// An agenda item is owned by its author and reaches every гурток it is assigned to, so a
    /// Виховник moderates anything targeting a group he leads.
    /// </summary>
    private async Task<ResourceScope?> GetAgendaItemScopeAsync(Guid agendaItemKey, CancellationToken cancellationToken)
    {
        var item = await _context.AgendaItems
            .Where(a => a.AgendaItemKey == agendaItemKey)
            .Select(a => new { a.KurinKey, a.CreatedByUserKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        var groupKeys = await _context.AgendaAssignments
            .Where(a => a.AgendaItemKey == agendaItemKey && a.TargetType == AgendaTargetType.Group)
            .Select(a => a.TargetKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ResourceScope(item.KurinKey, null, item.CreatedByUserKey, groupKeys);
    }

    /// <summary>
    /// The journal is the kurin's, and it reaches every гурток in it: an own-group reader who leads
    /// any of them may open it, and the read itself narrows the entries to his гуртки. A scope of
    /// the kurin alone would refuse every own-group reader at the door.
    /// </summary>
    private async Task<ResourceScope?> GetJournalScopeAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        if (!await _context.Kurins.AnyAsync(k => k.KurinKey == kurinKey, cancellationToken))
        {
            return null;
        }

        var groupKeys = await _context.Groups
            .Where(g => g.KurinKey == kurinKey)
            .Select(g => g.GroupKey)
            .ToListAsync(cancellationToken);

        return new ResourceScope(kurinKey, null, null, groupKeys);
    }

    private async Task<ResourceScope?> GetLeadershipScopeAsync(Guid leadershipKey, CancellationToken cancellationToken)
    {
        var leadership = await _context.Leaderships
            .Where(l => l.LeadershipKey == leadershipKey)
            .Select(l => new { l.KurinKey, l.GroupKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (leadership is null)
        {
            return null;
        }

        if (leadership.KurinKey.HasValue)
        {
            return new ResourceScope(leadership.KurinKey.Value, leadership.GroupKey, null);
        }

        if (!leadership.GroupKey.HasValue)
        {
            return null;
        }

        return await _context.Groups
            .Where(g => g.GroupKey == leadership.GroupKey.Value)
            .Select(g => new ResourceScope(g.KurinKey, g.GroupKey, null))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
