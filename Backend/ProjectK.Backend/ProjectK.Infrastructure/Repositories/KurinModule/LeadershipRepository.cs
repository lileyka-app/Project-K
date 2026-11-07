using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
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
                                .ThenInclude(h => h.Member)
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
                                .ThenInclude(h => h.Member)
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
                             .Include(h => h.Member)
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }

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
                    || (l.GroupKey != null && l.Group!.KurinKey == kurinKey)));

        var held = await (from ms in belongsHere
                          join history in _context.LeadershipHistories.Where(h => h.EndDate == null)
                              on ms.MemberKey equals history.MemberKey
                          join office in officesOfThisKurin
                              on history.LeadershipKey equals office.LeadershipKey
                          select new MemberOffice(office.Type, history.Role))
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var mentorsHere = await _context.MentorAssignments
            .AnyAsync(
                assignment => assignment.MentorUserKey == userKey
                    && assignment.RevokedAtUtc == null
                    && assignment.Group.KurinKey == kurinKey,
                cancellationToken);

        var vykhovnyk = new MemberOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
        if (mentorsHere && !held.Contains(vykhovnyk))
        {
            held.Add(vykhovnyk);
        }

        return held;
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

    public async Task<IReadOnlyList<MemberLookupDto>> GetOfficeMembersLookupAsync(
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

        var lookup = rows
            .Select(r => new MemberLookupDto
            {
                MemberKey = r.MemberKey,
                UserKey = r.UserKey,
                FirstName = r.FirstName,
                MiddleName = r.MiddleName,
                LastName = r.LastName,
                ProfilePhotoBlobName = r.ProfilePhotoBlobName,
                // The system-role name, not the bare enum: MemberLookupDto.UserRole is read as an
                // office identity, and MemberRepository fills the same field the same way.
                UserRole = SystemRole.ForOffice(type, r.Role)
            })
            .ToList();

        // A person closed over a гурток is a впорядник of this КВ whether or not the провід screen
        // ever seated them: access already reads the assignment that way, and the КВ list has to
        // show the same person the sidebar does.
        if (type == LeadershipType.KV)
        {
            var seated = lookup.Select(entry => entry.MemberKey).ToHashSet();
            var mentors = await _context.MentorAssignments
                .Where(assignment => assignment.RevokedAtUtc == null && assignment.Group.KurinKey == kurinKey)
                .Join(_context.Members,
                      assignment => assignment.MentorUserKey,
                      member => member.UserKey,
                      (assignment, member) => new { member.MemberKey, member.UserKey, member.FirstName, member.MiddleName, member.LastName, member.ProfilePhotoBlobName })
                .Distinct()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            lookup.AddRange(mentors
                .Where(mentor => seated.Add(mentor.MemberKey))
                .Select(mentor => new MemberLookupDto
                {
                    MemberKey = mentor.MemberKey,
                    UserKey = mentor.UserKey,
                    FirstName = mentor.FirstName,
                    MiddleName = mentor.MiddleName,
                    LastName = mentor.LastName,
                    ProfilePhotoBlobName = mentor.ProfilePhotoBlobName,
                    UserRole = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk)
                }));
        }

        return lookup;
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
