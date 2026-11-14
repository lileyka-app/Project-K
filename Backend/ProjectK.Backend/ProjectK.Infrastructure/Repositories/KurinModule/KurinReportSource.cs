using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public sealed class KurinReportSource : IKurinReportSource
{
    private readonly AppDbContext _context;

    public KurinReportSource(AppDbContext context)
    {
        _context = context;
    }

    public async Task<KurinReportSourceData?> LoadAsync(
        Guid kurinKey,
        Guid? currentUserKey,
        CancellationToken cancellationToken = default)
    {
        var kurin = await _context.Kurins
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.KurinKey == kurinKey, cancellationToken);

        if (kurin is null)
        {
            return null;
        }

        var groups = await _context.Groups
            .AsNoTracking()
            .Where(group => group.KurinKey == kurinKey)
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);

        var groupKeys = groups.Select(group => group.GroupKey).ToArray();

        var mentorAssignments = await _context.MentorAssignments
            .AsNoTracking()
            .Where(assignment => groupKeys.Contains(assignment.GroupKey) && assignment.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        // Who the report is about is decided by membership, and the membership is also what says
        // which гурток each of them is in — the person's own record no longer carries either.
        var memberships = await _context.Memberships
            .AsNoTracking()
            .Where(membership => membership.KurinKey == kurinKey && membership.LeftAtUtc == null)
            .ToListAsync(cancellationToken);

        var memberKeysHere = memberships.Select(membership => membership.MemberKey).ToArray();

        var members = await _context.Members
            .AsNoTracking()
            .AsSplitQuery()
            .Where(member => memberKeysHere.Contains(member.MemberKey))
            .Include(member => member.PlastLevelHistory)
            .Include(member => member.MemberWarnings)
            .Include(member => member.MemberAwards)
            .Include(member => member.LeadershipHistories)
                .ThenInclude(history => history.Leadership)
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToListAsync(cancellationToken);

        var userKeys = members
            .Select(member => member.UserKey)
            .OfType<Guid>()
            .Concat(mentorAssignments.Select(assignment => assignment.MentorUserKey))
            .Concat(currentUserKey is Guid userId ? [userId] : [])
            .Distinct()
            .ToArray();

        var usersByKey = await _context.Users
            .AsNoTracking()
            .Where(user => userKeys.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        // Ролі більше не читаються тут окремим запитом. Вони вже приходять із людиною
        // (`Include(LeadershipHistories).ThenInclude(Leadership)`), а звіт друкує їх словами, не
        // системними ролями — тож той запит рахував те саме вдруге, аби перекласти назад у
        // "KV.Vykhovnyk".
        return new KurinReportSourceData(
            kurin,
            groups,
            mentorAssignments,
            members,
            (IReadOnlyDictionary<Guid, AppUser>)usersByKey,
            memberships.ToDictionary(membership => membership.MemberKey));
    }
}
