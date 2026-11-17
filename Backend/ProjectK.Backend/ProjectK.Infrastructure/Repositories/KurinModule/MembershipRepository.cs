using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MembershipRepository : BaseEntityRepository<Membership>, IMembershipRepository
{
    public MembershipRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>Every membership that is still open.</summary>
    private IQueryable<Membership> OpenMemberships => Context.Memberships.Where(ms => ms.LeftAtUtc == null);

    public Task<IReadOnlyList<MemberListRow>> GetRosterRowsByKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => ProjectRosterRowsAsync(OpenMemberships.Where(ms => ms.KurinKey == kurinKey), cancellationToken);

    public Task<IReadOnlyList<MemberListRow>> GetRosterRowsByGroupAsync(Guid groupKey, CancellationToken cancellationToken = default)
        => ProjectRosterRowsAsync(OpenMemberships.Where(ms => ms.GroupKey == groupKey), cancellationToken);

    // Single projection shared by the kurin- and group-scoped roster reads. It reads a list of
    // memberships and joins the person behind each across the module line — a read, never a write: the same member seen from two kurins is two
    // entries, each placed by its own membership. One SQL, no Include graph. Every field is read as
    // stored: who is кадра and which office names them is RegistryMapping's to say, what the caller
    // may see is GetMembersQuery's to mask.
    private async Task<IReadOnlyList<MemberListRow>> ProjectRosterRowsAsync(
        IQueryable<Membership> memberships,
        CancellationToken cancellationToken)
    {
        var source = from ms in memberships
                     join person in Context.Members on ms.MemberKey equals person.MemberKey
                     select new { Placement = ms, Person = person };

        return await source
            .Select(x => new MemberListRow
            {
                MemberKey = x.Person.MemberKey,
                GroupKey = x.Placement.GroupKey,
                GroupName = Context.Groups.Where(g => g.GroupKey == x.Placement.GroupKey).Select(g => g.Name).FirstOrDefault(),
                KurinKey = x.Placement.KurinKey,
                UserKey = x.Person.UserKey,
                FirstName = x.Person.FirstName,
                MiddleName = x.Person.MiddleName,
                LastName = x.Person.LastName,
                Email = x.Person.Email,
                PhoneNumber = x.Person.PhoneNumber,
                DateOfBirth = x.Person.DateOfBirth,
                Address = x.Person.Address,
                School = x.Person.School,
                StoredPlastLevel = x.Person.LatestPlastLevel,
                ProfilePhotoBlobName = x.Person.ProfilePhotoBlobName,
                ProfileVerificationStatus = x.Person.ProfileVerificationStatus,
                ProfileVerifiedAtUtc = x.Person.ProfileVerifiedAtUtc,
                ProfileVerifiedByUserKey = x.Person.ProfileVerifiedByUserKey,
                ProfileVerificationNote = x.Person.ProfileVerificationNote,
                PlastLevels = x.Person.PlastLevelHistory.ToList(),
                // Offices of this kurin only: a role held in another kurin is not this kurin's to
                // show, and would be named by this kurin's own role names.
                Offices = Context.LeadershipHistories
                    .Where(h => h.MemberKey == x.Person.MemberKey
                        && h.EndDate == null
                        && h.Leadership.EndDate == null
                        && (h.Leadership.KurinKey == x.Placement.KurinKey
                            || Context.Groups.Any(g => g.GroupKey == h.Leadership.GroupKey && g.KurinKey == x.Placement.KurinKey)))
                    .Select(h => new MemberListOffice(
                        h.LeadershipHistoryKey,
                        h.MemberKey,
                        h.LeadershipKey,
                        h.Role,
                        h.Leadership.Type,
                        h.Leadership.KurinKey,
                        Context.Groups.Where(g => g.GroupKey == h.Leadership.GroupKey).Select(g => g.Name).FirstOrDefault(),
                        h.StartDate,
                        h.EndDate,
                        h.Leadership.EndDate))
                    .ToList(),
                // Закріплення виховника ведеться на акаунт, а не на людину, тож людина без
                // акаунта не має закріплень — і список порожній, а не помилковий.
                Assignments = (from assignment in Context.MentorAssignments
                               join assigned in Context.Groups on assignment.GroupKey equals assigned.GroupKey
                               where x.Person.UserKey != null
                                   && assignment.MentorUserKey == x.Person.UserKey
                                   && assigned.KurinKey == x.Placement.KurinKey
                               orderby assigned.Name
                               select new MemberListAssignment(assigned.Name, assigned.KurinKey, assignment.RevokedAtUtc))
                    .ToList(),
                Warnings = x.Person.MemberWarnings
                    .Where(w => w.RevokedAtUtc == null)
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberLookupRow>> GetMentorCandidatesAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        // One row per candidate, with the first office they hold in this kurin. It used to fan out
        // over the identity store's roles and collapse them again, which also meant showing an
        // office earned in a different kurin.
        var rows = await Context.Members
            .Where(m => OpenMemberships.Any(ms => ms.KurinKey == kurinKey && ms.MemberKey == m.MemberKey))
            .Where(m => m.UserKey != null)
            .Select(m => new
            {
                m.MemberKey,
                m.UserKey,
                m.FirstName,
                m.MiddleName,
                m.LastName,
                m.ProfilePhotoBlobName,
                // Read as its two halves: the enums are stored as numbers, so the office is put
                // together after the query.
                Office = (from history in Context.LeadershipHistories
                          where history.MemberKey == m.MemberKey && history.EndDate == null
                          join office in Context.Leaderships
                              on history.LeadershipKey equals office.LeadershipKey
                          where office.EndDate == null
                              && (office.KurinKey == kurinKey
                                  || Context.Groups.Any(g => g.GroupKey == office.GroupKey && g.KurinKey == kurinKey))
                          orderby office.Type, history.Role
                          select new { office.Type, history.Role }).FirstOrDefault()
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MemberLookupRow(
                row.MemberKey,
                row.UserKey,
                row.FirstName,
                row.MiddleName,
                row.LastName,
                row.ProfilePhotoBlobName,
                row.Office is null ? null : new MemberOffice(row.Office.Type, row.Office.Role)))
            .ToList();
    }


    public async Task<IReadOnlyCollection<Membership>> GetActiveForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
        => await Context.Memberships
            .AsNoTracking()
            .Where(m => m.MemberKey == memberKey && m.LeftAtUtc == null)
            .OrderByDescending(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MembershipRecord>> GetRecordsForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
        => await AsRecords(Context.Memberships.Where(m => m.MemberKey == memberKey))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetRecordsForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
    {
        var rows = Context.Memberships.Where(m => memberKeys.Contains(m.MemberKey));
        var owners = await rows.Select(m => new { m.MembershipKey, m.MemberKey }).ToDictionaryAsync(m => m.MembershipKey, m => m.MemberKey, cancellationToken);
        var records = await AsRecords(rows).ToListAsync(cancellationToken);
        return records
            .GroupBy(record => owners[record.MembershipKey])
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<MembershipRecord>)group.ToList());
    }

    public async Task<IReadOnlyCollection<FormerMember>> GetFormerInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
    {
        var closed = Context.Memberships.Where(m => m.KurinKey == kurinKey && m.LeftAtUtc != null);

        // Someone can leave, be taken back and leave again — the spell that matters is the last
        // one. And someone taken back who is still here is not former at all, however many closed
        // rows they have behind them.
        var stillHere = Context.Memberships
            .Where(m => m.KurinKey == kurinKey && m.LeftAtUtc == null)
            .Select(m => m.MemberKey);

        var rows = await (
                from membership in closed
                where !stillHere.Contains(membership.MemberKey)
                join person in Context.Members on membership.MemberKey equals person.MemberKey
                select new
                {
                    membership.MemberKey,
                    person.FirstName,
                    person.MiddleName,
                    person.LastName,
                    membership.GroupKey,
                    GroupName = Context.Groups.Where(g => g.GroupKey == membership.GroupKey).Select(g => g.Name).FirstOrDefault(),
                    membership.JoinedAtUtc,
                    LeftAtUtc = membership.LeftAtUtc!.Value
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.MemberKey)
            .Select(spells => spells.OrderByDescending(row => row.LeftAtUtc).First())
            .Select(row => new FormerMember(
                row.MemberKey,
                row.FirstName,
                row.MiddleName,
                row.LastName,
                row.GroupKey,
                row.GroupName,
                row.JoinedAtUtc,
                row.LeftAtUtc))
            .OrderByDescending(person => person.LeftAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyCollection<MembershipRecord>> GetCurrentRecordsForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default)
        => await AsRecords(Context.Memberships.Where(m => m.UserKey == userKey && m.LeftAtUtc == null))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetCurrentRecordsForAccountsAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default)
    {
        if (userKeys.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyCollection<MembershipRecord>>();
        }

        var keys = userKeys.Distinct().ToList();
        var rows = await Context.Memberships
            .Where(m => m.UserKey != null && keys.Contains(m.UserKey.Value) && m.LeftAtUtc == null)
            .Join(Context.Kurins, m => m.KurinKey, kurin => kurin.KurinKey, (m, kurin) => new { m, kurin })
            .OrderByDescending(x => x.m.JoinedAtUtc)
            .Select(x => new
            {
                UserKey = x.m.UserKey!.Value,
                Record = new MembershipRecord(
                    x.m.MembershipKey,
                    x.m.KurinKey,
                    x.kurin.Number,
                    x.kurin.Branch,
                    x.kurin.NamedAfter,
                    x.m.GroupKey,
                    Context.Groups.Where(group => group.GroupKey == x.m.GroupKey).Select(group => group.Name).FirstOrDefault(),
                    x.m.Kind,
                    x.m.JoinedAtUtc,
                    x.m.LeftAtUtc,
                    x.kurin.Branches.Select(b => new KurinNumbering(b.Branch, b.Number)).ToList())
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserKey)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<MembershipRecord>)group.Select(row => row.Record).ToList());
    }

    public async Task<IReadOnlyCollection<Guid>> GetAccountKeysInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => await Context.Memberships
            .Where(m => m.KurinKey == kurinKey && m.LeftAtUtc == null && m.UserKey != null)
            .Select(m => m.UserKey!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<int> CountCurrentInKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Context.Memberships.CountAsync(m => m.KurinKey == kurinKey && m.LeftAtUtc == null, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountCurrentInKurinsAsync(
        IReadOnlyCollection<Guid> kurinKeys,
        CancellationToken cancellationToken = default)
    {
        if (kurinKeys.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var keys = kurinKeys.Distinct().ToList();
        return await Context.Memberships
            .Where(m => keys.Contains(m.KurinKey) && m.LeftAtUtc == null)
            .GroupBy(m => m.KurinKey)
            .Select(group => new { KurinKey = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.KurinKey, row => row.Count, cancellationToken);
    }

    public async Task<IReadOnlyCollection<KurinMembershipRecord>> GetRecordsInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => await Context.Memberships
            .AsNoTracking()
            .Where(m => m.KurinKey == kurinKey)
            .Select(m => new KurinMembershipRecord(m.MembershipKey, m.MemberKey, m.GroupKey, m.Kind, m.JoinedAtUtc, m.LeftAtUtc))
            .ToListAsync(cancellationToken);

    /// <summary>Current memberships first, then the ones already closed, newest joined first.</summary>
    private IQueryable<MembershipRecord> AsRecords(IQueryable<Membership> memberships)
        => memberships
            .AsNoTracking()
            .Join(Context.Kurins, m => m.KurinKey, kurin => kurin.KurinKey, (m, kurin) => new { m, kurin })
            .OrderBy(x => x.m.LeftAtUtc == null ? 0 : 1)
            .ThenByDescending(x => x.m.JoinedAtUtc)
            .Select(x => new MembershipRecord(
                x.m.MembershipKey,
                x.m.KurinKey,
                x.kurin.Number,
                x.kurin.Branch,
                x.kurin.NamedAfter,
                x.m.GroupKey,
                Context.Groups.Where(group => group.GroupKey == x.m.GroupKey).Select(group => group.Name).FirstOrDefault(),
                x.m.Kind,
                x.m.JoinedAtUtc,
                x.m.LeftAtUtc,
                x.kurin.Branches.Select(b => new KurinNumbering(b.Branch, b.Number)).ToList()));

    public Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Context.Memberships.CountAsync(m => m.MemberKey == memberKey, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetKurinKeysForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default)
        => await Context.Memberships
            .Where(m => m.UserKey == userKey && m.LeftAtUtc == null)
            .Select(m => m.KurinKey)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task SyncAccountAsync(
        Guid memberKey,
        Guid? userKey,
        CancellationToken cancellationToken = default)
    {
        foreach (var membership in await CurrentOfAsync(memberKey, cancellationToken))
        {
            membership.UserKey = userKey;
        }
    }

    public Task<Membership?> GetActiveAsync(
        Guid memberKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => Context.Memberships.FirstOrDefaultAsync(
            m => m.MemberKey == memberKey && m.KurinKey == kurinKey && m.LeftAtUtc == null,
            cancellationToken);

    public void Open(Membership membership) => Context.Memberships.Add(membership);

    public async Task PlaceAsync(
        Guid memberKey,
        Guid? userKey,
        Guid kurinKey,
        Guid? groupKey,
        CancellationToken cancellationToken = default)
    {
        var here = (await CurrentOfAsync(memberKey, cancellationToken)).FirstOrDefault(m => m.KurinKey == kurinKey);

        if (here is null)
        {
            Context.Memberships.Add(new Membership
            {
                MemberKey = memberKey,
                UserKey = userKey,
                KurinKey = kurinKey,
                GroupKey = groupKey,
                JoinedAtUtc = DateTime.UtcNow
            });
            return;
        }

        here.GroupKey = groupKey;
        here.UserKey = userKey;
    }

    public async Task<IReadOnlyCollection<Guid>> RemoveForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return [];
        }

        var rows = await Context.Memberships
            .Where(m => memberKeys.Contains(m.MemberKey))
            .ToListAsync(cancellationToken);
        Context.Memberships.RemoveRange(rows);
        return rows.Select(m => m.MembershipKey).ToList();
    }

    public async Task RemoveForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Context.Memberships.RemoveRange(
            await Context.Memberships
                .Where(m => m.KurinKey == kurinKey)
                .ToListAsync(cancellationToken));

    public async Task DetachFromGroupAsync(Guid groupKey, CancellationToken cancellationToken = default)
    {
        var inGroup = await Context.Memberships
            .Where(m => m.GroupKey == groupKey)
            .ToListAsync(cancellationToken);

        foreach (var membership in inGroup)
        {
            membership.GroupKey = null;
        }
    }

    /// <summary>
    /// A person's open memberships as tracked entities, including any opened earlier in this same
    /// unit of work — a member created and placed in one request has no row in the database yet.
    /// </summary>
    private async Task<List<Membership>> CurrentOfAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        var stored = await Context.Memberships
            .Where(m => m.MemberKey == memberKey && m.LeftAtUtc == null)
            .ToListAsync(cancellationToken);

        var pending = Context.Memberships.Local
            .Where(m => m.MemberKey == memberKey && m.LeftAtUtc == null)
            .Where(m => !stored.Contains(m));

        return [.. stored, .. pending];
    }
}
