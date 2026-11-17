using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MentorAssignmentRepository : BaseEntityRepository<MentorAssignment>, IMentorAssignmentRepository
{

    public MentorAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments.AnyAsync(ma => ma.MentorAssignmentKey == entityKey, cancellationToken);
    }

    public async Task<IEnumerable<MentorAssignment>> GetByGroupKeyAsync(Guid groupKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments
            .Where(ma => ma.GroupKey == groupKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MentorAssignment>> GetByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments
            .Where(ma => Context.Groups.Any(group => group.GroupKey == ma.GroupKey && group.KurinKey == kurinKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GroupRef>> GetActiveGroupsAsync(Guid mentorUserKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        // A record cannot be translated under Distinct/OrderBy, so the pair is read flat and wrapped after.
        var groups = await Context.MentorAssignments
            .Join(Context.Groups, ma => ma.GroupKey, group => group.GroupKey, (ma, group) => new { ma, group })
            .Where(pair => pair.ma.MentorUserKey == mentorUserKey && pair.ma.RevokedAtUtc == null && pair.group.KurinKey == kurinKey)
            .Select(pair => new { pair.ma.GroupKey, pair.group.Name })
            .Distinct()
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(group => new GroupRef(group.GroupKey, group.Name)).ToList();
    }

    public Task<bool> HasActiveInKurinAsync(Guid mentorUserKey, Guid kurinKey, CancellationToken cancellationToken = default)
        => (from assignment in Context.MentorAssignments
            join assigned in Context.Groups on assignment.GroupKey equals assigned.GroupKey
            where assignment.MentorUserKey == mentorUserKey
                && assignment.RevokedAtUtc == null
                && assigned.KurinKey == kurinKey
            select assignment)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberLookupRow>> GetActiveMentorsInKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var mentors = await (from assignment in Context.MentorAssignments
                             join assigned in Context.Groups on assignment.GroupKey equals assigned.GroupKey
                             join member in Context.Members on assignment.MentorUserKey equals member.UserKey
                             where assignment.RevokedAtUtc == null && assigned.KurinKey == kurinKey
                             select new { member.MemberKey, member.UserKey, member.FirstName, member.MiddleName, member.LastName, member.ProfilePhotoBlobName })
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return mentors
            .Select(mentor => new MemberLookupRow(
                mentor.MemberKey,
                mentor.UserKey,
                mentor.FirstName,
                mentor.MiddleName,
                mentor.LastName,
                mentor.ProfilePhotoBlobName,
                null))
            .ToList();
    }

    public override async Task<MentorAssignment?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments
            .FirstOrDefaultAsync(ma => ma.MentorAssignmentKey == entityKey, cancellationToken);
    }

    public async Task<IEnumerable<MentorAssignment>> GetByMentorUserKeyAsync(Guid mentorUserKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments
            .Where(ma => ma.MentorUserKey == mentorUserKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<MentorAssignment?> GetSpecificAssignmentAsync(Guid mentorUserKey, Guid groupKey, CancellationToken cancellationToken = default)
    {
        return await Context.MentorAssignments
            .Where(ma => ma.MentorUserKey == mentorUserKey && ma.GroupKey == groupKey)
            .OrderBy(ma => ma.RevokedAtUtc == null ? 0 : 1)
            .ThenByDescending(ma => ma.RevokedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

}
