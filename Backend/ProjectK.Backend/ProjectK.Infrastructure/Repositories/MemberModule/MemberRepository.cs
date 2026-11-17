using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.MemberModule;

public class MemberRepository : BaseEntityRepository<Member>, IMemberRepository
{
    private readonly string memberMessage = "Member not found.";
    public MemberRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>Every membership that is still open; read only to count, for the public-code card.</summary>
    private IQueryable<Membership> ActiveMemberships => Context.Memberships.Where(ms => ms.LeftAtUtc == null);

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
    /// with ten ступенів, two пересторог and four відзначень comes back as 80
    /// rows carrying the same person over and over. Each collection gets its own query instead.
    /// </para>
    /// </summary>
    public override async Task<Member?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        var member = await Context.Members.AsSplitQuery()
                                     .Include(m => m.PlastLevelHistory)
                                     .Include(m => m.MemberWarnings)
                                     .Include(m => m.MemberAwards)
                                     .FirstOrDefaultAsync(e => e.MemberKey == entityKey, cancellationToken);
        return member;
    }

    public async Task<IReadOnlyList<Member>> GetManyAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
        => await Context.Members
            .AsNoTracking()
            .Where(m => memberKeys.Contains(m.MemberKey))
            .ToListAsync(cancellationToken);

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

    /// <summary>People as another module sees them: named and linked to their account, nothing more.</summary>
    private static IQueryable<MemberSummary> SummariesOf(IQueryable<Member> people)
        => people.Select(m => new MemberSummary(
            m.MemberKey,
            m.UserKey,
            m.FirstName,
            m.LastName,
            m.Email,
            m.ProfilePhotoBlobName));

    public Task<MemberSummary?> GetSummaryByKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => SummariesOf(Context.Members.Where(m => m.MemberKey == memberKey))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MemberSummary?> GetSummaryByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default)
        => SummariesOf(Context.Members.Where(m => m.UserKey == userKey))
            .FirstOrDefaultAsync(cancellationToken);

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
