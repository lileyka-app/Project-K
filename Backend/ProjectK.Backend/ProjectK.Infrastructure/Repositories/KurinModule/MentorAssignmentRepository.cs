using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
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
            .Include(ma => ma.Group)
            .Where(ma => ma.Group.KurinKey == kurinKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GroupRef>> GetActiveGroupsAsync(Guid mentorUserKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        // A record cannot be translated under Distinct/OrderBy, so the pair is read flat and wrapped after.
        var groups = await Context.MentorAssignments
            .Where(ma => ma.MentorUserKey == mentorUserKey && ma.RevokedAtUtc == null && ma.Group.KurinKey == kurinKey)
            .Select(ma => new { ma.GroupKey, ma.Group.Name })
            .Distinct()
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(group => new GroupRef(group.GroupKey, group.Name)).ToList();
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
