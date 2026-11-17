using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.InfrastructureModule;

namespace ProjectK.Infrastructure.Tests.InfrastructureModule;

/// <summary>
/// The repository hands over stored facts and the rules decide. These are the cases the repository
/// used to decide itself, with the answers it gave then: the rule over the facts must give them still.
/// </summary>
public sealed class ResourceScopeRepositoryFactsTests : IDisposable
{
    private readonly AppDbContext _context = new(new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    public void Dispose() => _context.Dispose();

    private Guid KurinIn(params KurinBranch[] branches)
    {
        var kurinKey = Guid.NewGuid();
        foreach (var branch in branches)
        {
            _context.Set<KurinBranchNumber>().Add(new KurinBranchNumber { KurinKey = kurinKey, Branch = branch, Number = 7 });
        }

        return kurinKey;
    }

    private void Join(Guid memberKey, Guid kurinKey, MembershipKind kind, Guid? groupKey = null, Guid? userKey = null) =>
        _context.Memberships.Add(new Membership
        {
            MemberKey = memberKey,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            UserKey = userKey,
            Kind = kind,
            JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
        });

    private async Task<bool> SeniorAsync(Guid memberKey) =>
        SeniorStandingRules.StandsInSeniorKurin(
            await new ResourceScopeRepository(_context).GetOpenMembershipBranchesAsync(ResourceType.Member, memberKey));

    [Fact]
    public async Task ASeniorServingAsStaffInAYouthKurin_IsStillASenior()
    {
        var person = Guid.NewGuid();
        Join(person, KurinIn(KurinBranch.UPYu), MembershipKind.Staff);
        Join(person, KurinIn(KurinBranch.USP, KurinBranch.UPS), MembershipKind.Youth);
        await _context.SaveChangesAsync();

        Assert.Equal(3, (await new ResourceScopeRepository(_context).GetOpenMembershipBranchesAsync(ResourceType.Member, person)).Count);
        Assert.True(await SeniorAsync(person));
    }

    [Fact]
    public async Task StaffInAYouthKurinAlone_IsNoSenior()
    {
        var person = Guid.NewGuid();
        Join(person, KurinIn(KurinBranch.UPYu), MembershipKind.Staff);
        await _context.SaveChangesAsync();

        Assert.False(await SeniorAsync(person));
    }

    [Fact]
    public async Task AYouthInASeniorKurin_IsASenior_UntilTheyAreStillAYouthInAnUpyuKurin()
    {
        var onlySenior = Guid.NewGuid();
        var alsoUpyuYouth = Guid.NewGuid();
        var senior = KurinIn(KurinBranch.USP);
        var upyu = KurinIn(KurinBranch.UPYu);
        Join(onlySenior, senior, MembershipKind.Youth);
        Join(alsoUpyuYouth, senior, MembershipKind.Youth);
        Join(alsoUpyuYouth, upyu, MembershipKind.Youth);
        await _context.SaveChangesAsync();

        Assert.True(await SeniorAsync(onlySenior));
        Assert.False(await SeniorAsync(alsoUpyuYouth));
    }

    [Fact]
    public async Task ResourcesNotAboutAPerson_HaveNoStanding()
    {
        var kurin = KurinIn(KurinBranch.USP);
        Join(Guid.NewGuid(), kurin, MembershipKind.Youth);
        await _context.SaveChangesAsync();

        Assert.Empty(await new ResourceScopeRepository(_context).GetOpenMembershipBranchesAsync(ResourceType.Kurin, kurin));
    }

    [Fact]
    public async Task LedGroups_AreOffices_ThenMentorAssignmentsWithoutAnOffice_ThenTheOwnGroup_EachOnce()
    {
        var userKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var seated = new Group("Орли", kurinKey);
        var mentored = new Group("Сови", kurinKey);
        var own = new Group("Лиси", kurinKey);
        var formerlySeated = new Group("Вовки", kurinKey);
        _context.Groups.AddRange(seated, mentored, own, formerlySeated);
        Join(memberKey, kurinKey, MembershipKind.Staff, own.GroupKey, userKey);
        Seat(memberKey, seated.GroupKey, endDate: null);
        Seat(memberKey, own.GroupKey, endDate: null);
        Seat(memberKey, formerlySeated.GroupKey, endDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        _context.MentorAssignments.Add(new MentorAssignment { MentorUserKey = userKey, GroupKey = mentored.GroupKey, AssignedAtUtc = DateTime.UtcNow });
        _context.MentorAssignments.Add(new MentorAssignment { MentorUserKey = userKey, GroupKey = formerlySeated.GroupKey, AssignedAtUtc = DateTime.UtcNow, RevokedAtUtc = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var sources = await new ResourceScopeRepository(_context).GetLedGroupSourcesAsync(userKey, kurinKey);

        Assert.Equal(new[] { seated.GroupKey, own.GroupKey }.Order(), sources.OfficeGroupKeys.Order());
        Assert.Equal([mentored.GroupKey], sources.MentorGroupKeys);
        Assert.Equal(own.GroupKey, sources.OwnGroupKey);
        Assert.Equal(new[] { seated.GroupKey, own.GroupKey, mentored.GroupKey }.Order(), LedGroupRules.LedGroupKeys(sources).Order());
    }

    [Fact]
    public async Task LedGroups_FallBackToTheOwnGroup_WithNoOfficeAndNoAssignment()
    {
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var own = Guid.NewGuid();
        Join(Guid.NewGuid(), kurinKey, MembershipKind.Youth, own, userKey);
        await _context.SaveChangesAsync();

        var sources = await new ResourceScopeRepository(_context).GetLedGroupSourcesAsync(userKey, kurinKey);

        Assert.Empty(sources.OfficeGroupKeys);
        Assert.Empty(sources.MentorGroupKeys);
        Assert.Equal([own], LedGroupRules.LedGroupKeys(sources));
    }

    private void Seat(Guid memberKey, Guid groupKey, DateOnly? endDate)
    {
        var leadership = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Group, GroupKey = groupKey, StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)) };
        _context.Leaderships.Add(leadership);
        _context.LeadershipHistories.Add(new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            MemberKey = memberKey,
            LeadershipKey = leadership.LeadershipKey,
            Role = LeadershipRole.Hurtkoviy,
            StartDate = leadership.StartDate,
            EndDate = endDate
        });
    }
}
