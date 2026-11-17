using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.MemberModule;

public class MemberRepository : BaseEntityRepository<Member>, IMemberRepository
{
    private readonly string memberMessage = "Member not found.";
    public MemberRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Every membership that is still open. Where a person stands in a kurin is said here and
    /// nowhere else, so every read that means "the people of this kurin" or "of this гурток"
    /// starts from this and not from the columns still left on the member record.
    /// </summary>
    private IQueryable<Membership> ActiveMemberships => Context.Memberships.Where(ms => ms.LeftAtUtc == null);

    /// <summary>
    /// The people behind a set of memberships, as a query still rooted in <c>Members</c> — a join
    /// would project the entity out of its own query and take <c>Include</c> with it.
    /// </summary>
    private IQueryable<Member> PeopleOf(IQueryable<Membership> memberships)
        => Context.Members.Where(m => memberships.Any(ms => ms.MemberKey == m.MemberKey));

    public override void Create(Member member, CancellationToken cancellationToken = default)
    {
        Context.Members.Add(member);
    }

    public override void Delete(Member member, CancellationToken cancellationToken = default)
    {
        Context.Members.Remove(member);
    }

    /// <summary>
    /// One person with everything their card shows.
    /// <para>
    /// Split, not joined: four collections in one query multiply against each other, so a person
    /// with ten ступенів, three uryadiv, two pересторог and four відзначень comes back as 240
    /// rows carrying the same person over and over. Each collection gets its own query instead.
    /// </para>
    /// </summary>
    public override async Task<Member?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        var member = await Context.Members.AsSplitQuery()
                                     .Include(m => m.PlastLevelHistory)
                                     .Include(m => m.LeadershipHistories)
                                        .ThenInclude(h => h.Leadership)
                                     .Include(m => m.MemberWarnings)
                                     .Include(m => m.MemberAwards)
                                     .FirstOrDefaultAsync(e => e.MemberKey == entityKey, cancellationToken);
        return member;
    }

    /// <summary>The people currently placed in one гурток.</summary>
    public async Task<IEnumerable<Member>> GetAllAsync(Guid groupKey, CancellationToken cancellationToken = default)
    {
        return await PeopleOf(ActiveMemberships.Where(ms => ms.GroupKey == groupKey))
                            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Member>> GetAllByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await PeopleOf(ActiveMemberships.Where(ms => ms.KurinKey == kurinKey))
                                     .Include(m => m.PlastLevelHistory)
                                     .Include(m => m.LeadershipHistories)
                                        .ThenInclude(h => h.Leadership)
                                     .Include(m => m.MemberWarnings)
                                     .Include(m => m.MemberAwards)
                                     .AsSplitQuery()
                                     .AsNoTracking()
                                     .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<MemberListRow>> GetListRowsByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => ProjectListRowsAsync(ActiveMemberships.Where(ms => ms.KurinKey == kurinKey), cancellationToken);

    public Task<IReadOnlyList<MemberListRow>> GetListRowsByGroupKeyAsync(Guid groupKey, CancellationToken cancellationToken = default)
        => ProjectListRowsAsync(ActiveMemberships.Where(ms => ms.GroupKey == groupKey), cancellationToken);

    // Single projection shared by the kurin- and group-scoped list reads. It reads a list of
    // memberships and pulls the person behind each: the same member seen from two kurins is two
    // entries, each placed by its own membership. One SQL, no Include graph. Every field is read as
    // stored: who is кадра and which office names them is MemberMapping's to say, what the caller
    // may see is GetMembersQuery's to mask.
    private async Task<IReadOnlyList<MemberListRow>> ProjectListRowsAsync(
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
                Offices = x.Person.LeadershipHistories
                    .Where(h => h.EndDate == null
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

    public async Task<IReadOnlyCollection<MemberIdentity>> FindPossibleMatchesAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> phoneNumbers,
        IReadOnlyCollection<string> lastNames,
        CancellationToken cancellationToken = default)
    {
        var addresses = emails.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        var phones = phoneNumbers.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        var surnames = lastNames.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();

        if (addresses.Count == 0 && phones.Count == 0 && surnames.Count == 0)
        {
            return [];
        }

        return await Context.Members
            .AsNoTracking()
            .Where(member => addresses.Contains(member.Email)
                || phones.Contains(member.PhoneNumber)
                || surnames.Contains(member.LastName))
            .Select(member => new MemberIdentity(
                member.MemberKey,
                member.FirstName,
                member.LastName,
                member.Email,
                member.PhoneNumber,
                member.DateOfBirth))
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> GetUserKeyByMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Context.Members
            .Where(m => m.MemberKey == memberKey)
            .Select(m => m.UserKey)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// People as another module sees them, each placed by their most recent open membership.
    /// Someone who belongs to no kurin — an account activated but not yet joined to anything —
    /// still answers, with an empty kurin, because the caller asked about the person.
    /// </summary>
    private IQueryable<MemberSummary> SummariesOf(IQueryable<Member> people)
        => from m in people
           from ms in ActiveMemberships
               .Where(candidate => candidate.MemberKey == m.MemberKey)
               .OrderByDescending(candidate => candidate.JoinedAtUtc)
               .Take(1)
               .DefaultIfEmpty()
           select new MemberSummary(
               m.MemberKey,
               m.UserKey,
               ms == null ? Guid.Empty : ms.KurinKey,
               ms == null ? (Guid?)null : ms.GroupKey,
               m.FirstName,
               m.LastName,
               m.Email,
               m.ProfilePhotoBlobName);

    public Task<MemberSummary?> GetSummaryByKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => SummariesOf(Context.Members.Where(m => m.MemberKey == memberKey))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MemberSummary?> GetSummaryByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default)
        => SummariesOf(Context.Members.Where(m => m.UserKey == userKey))
            .FirstOrDefaultAsync(cancellationToken);

    // Asked with a kurin in hand, so the placement is that kurin's membership and not whichever
    // one happens to be newest — the same person read from another kurin answers differently.
    public async Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await (from ms in ActiveMemberships.Where(ms => ms.KurinKey == kurinKey)
                  join m in Context.Members on ms.MemberKey equals m.MemberKey
                  select new MemberSummary(
                      m.MemberKey,
                      m.UserKey,
                      ms.KurinKey,
                      ms.GroupKey,
                      m.FirstName,
                      m.LastName,
                      m.Email,
                      m.ProfilePhotoBlobName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKurinKeyAsync(Guid kurinKey, MemberSelection selection, CancellationToken cancellationToken = default)
    {
        if (selection.IsEmpty)
        {
            return [];
        }

        var memberKeys = selection.MemberKeys.ToList();
        var groupKeys = selection.GroupKeys.ToList();
        var accountKeys = selection.AccountKeys.ToList();

        return await (from ms in ActiveMemberships.Where(ms => ms.KurinKey == kurinKey)
                      join m in Context.Members on ms.MemberKey equals m.MemberKey
                      where memberKeys.Contains(m.MemberKey)
                          || (ms.GroupKey != null && groupKeys.Contains(ms.GroupKey.Value))
                          || (m.UserKey != null && accountKeys.Contains(m.UserKey.Value))
                      select new MemberSummary(
                          m.MemberKey,
                          m.UserKey,
                          ms.KurinKey,
                          ms.GroupKey,
                          m.FirstName,
                          m.LastName,
                          m.Email,
                          m.ProfilePhotoBlobName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MemberSummary>> GetAllSummariesAsync(CancellationToken cancellationToken = default)
        => await SummariesOf(Context.Members).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKeysAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
        => memberKeys.Count == 0
            ? []
            : await SummariesOf(Context.Members.Where(m => memberKeys.Contains(m.MemberKey))).ToListAsync(cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Context.Members.AnyAsync(m => m.Email == email, cancellationToken);

    public Task<MemberCard?> GetCardByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
        => Context.Members
            .Where(m => m.PublicId == publicId)
            .Select(m => new MemberCard(
                m.MemberKey,
                m.PublicId,
                m.FirstName,
                m.LastName,
                m.ProfilePhotoBlobName,
                // How many, never which: the count says "this person is already somewhere", which
                // is enough to give a провід pause, without naming the kurins.
                ActiveMemberships.Count(ms => ms.MemberKey == m.MemberKey)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MemberAccountLink?> GetAccountLinkAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Context.Members
            .Where(m => m.MemberKey == memberKey)
            .Select(m => new MemberAccountLink(m.MemberKey, m.UserKey))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetKurinKeyByMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => ActiveMemberships
            .Where(ms => ms.MemberKey == memberKey)
            .OrderByDescending(ms => ms.JoinedAtUtc)
            .Select(ms => (Guid?)ms.KurinKey)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberLookupRow>> GetMentorCandidatesAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        // One row per candidate, with the first office they hold in this kurin. It used to fan out
        // over the identity store's roles and collapse them again, which also meant showing an
        // office earned in a different kurin.
        var rows = await PeopleOf(ActiveMemberships.Where(ms => ms.KurinKey == kurinKey))
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

    public async Task<Member?> GetByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        return await Context.Members
            .Include(m => m.MemberWarnings)
            .Include(m => m.MemberAwards)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserKey == userKey, cancellationToken);
    }

    public async Task<Member?> GetTrackedByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        return await Context.Members
            .FirstOrDefaultAsync(m => m.UserKey == userKey, cancellationToken);
    }

    public async Task<Member?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await Context.Members
            .FirstOrDefaultAsync(m => m.Email == email, cancellationToken);
    }

    public override void Update(Member member, CancellationToken cancellationToken = default)
    {
        Context.Members.Update(member);
    }

    #region PlastLevelHistory Methods

    #endregion

    #region LeadershipHistory Methods

    #endregion
}
