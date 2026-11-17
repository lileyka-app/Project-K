using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public sealed class KurinReportRepository : IKurinReportRepository
{
    private readonly AppDbContext _context;

    public KurinReportRepository(AppDbContext context)
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
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToListAsync(cancellationToken);

        // Read beside the people, not through them: the offices are the kurin's, the people are not.
        var offices = (await _context.LeadershipHistories
                .AsNoTracking()
                .Include(history => history.Leadership)
                .Where(history => memberKeysHere.Contains(history.MemberKey))
                .ToListAsync(cancellationToken))
            .ToLookup(history => history.MemberKey);

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

        // Ролі більше не читаються тут окремим запитом. Вони приходять поруч із людьми
        // (`LeadershipHistories` з `Leadership`), а звіт друкує їх словами, не
        // системними ролями — тож той запит рахував те саме вдруге, аби перекласти назад у
        // "KV.Vykhovnyk".
        return new KurinReportSourceData(
            kurin,
            groups,
            mentorAssignments
                .Select(assignment => new KurinReportSourceMentor(assignment.MentorUserKey, assignment.GroupKey, assignment.RevokedAtUtc))
                .ToList(),
            members.Select(member => ToSource(member, offices[member.MemberKey])).ToList(),
            (IReadOnlyDictionary<Guid, AppUser>)usersByKey,
            memberships.ToDictionary(
                membership => membership.MemberKey,
                membership => new KurinMembershipRecord(
                    membership.MembershipKey,
                    membership.MemberKey,
                    membership.GroupKey,
                    membership.Kind,
                    membership.JoinedAtUtc,
                    membership.LeftAtUtc)));
    }

    private static KurinReportSourceMember ToSource(Member member, IEnumerable<LeadershipHistory> offices) => new(
        member.MemberKey,
        member.UserKey,
        member.FirstName,
        member.MiddleName,
        member.LastName,
        member.Email,
        member.PhoneNumber,
        member.DateOfBirth,
        member.Address,
        member.School,
        member.ProfilePhotoBlobName,
        member.LatestPlastLevel,
        member.PlastLevelHistory
            .Where(history => !history.AwaitsConfirmation)
            .Select(history => new KurinReportPlastLevel(history.PlastLevel, history.DateAchieved))
            .ToList(),
        member.MemberWarnings
            .Select(warning => new KurinReportSourceWarning(warning.Level, warning.IssuedAtUtc, warning.ExpiresAtUtc, warning.RevokedAtUtc))
            .ToList(),
        member.MemberAwards
            .Select(award => new KurinReportSourceAward(award.Level, award.CatalogId, award.DateAcquired, award.Note, award.Status))
            .ToList(),
        offices
            .Select(history => new KurinReportSourceOffice(
                history.Leadership.Type,
                history.Leadership.KurinKey,
                history.Leadership.GroupKey,
                history.Leadership.Name,
                history.Leadership.EndDate,
                history.Role,
                history.StartDate,
                history.EndDate))
            .ToList());
}
