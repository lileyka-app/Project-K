using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.InfrastructureModule;
using Xunit;

namespace ProjectK.Infrastructure.Tests.InfrastructureModule;

/// <summary>
/// The release's load-bearing claim, as a test: whether a person is within reach is answered by their
/// membership in the asking kurin, and by nothing else. Being in the system — even being someone's
/// виховник elsewhere — does not put them in scope here.
/// </summary>
public class ResourceScopeReaderMembershipTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Membership Joining(Guid memberKey, Guid kurinKey, Guid? groupKey, Guid? userKey) =>
        new()
        {
            MembershipKey = Guid.NewGuid(),
            MemberKey = memberKey,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            UserKey = userKey,
            Kind = MembershipKind.Youth,
            JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
        };

    [Fact]
    public async Task Member_ShouldBeInScope_OfTheKurinTheyBelongTo()
    {
        await using var context = NewContext();
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        context.Memberships.Add(Joining(memberKey, kurinKey, groupKey, userKey));
        await context.SaveChangesAsync();

        var scope = await new ResourceScopeReader(context)
            .GetScopeAsync(ResourceType.Member, memberKey, kurinKey);

        Assert.NotNull(scope);
        Assert.Equal(kurinKey, scope!.KurinKey);
        Assert.Equal(groupKey, scope.GroupKey);
        Assert.Equal(userKey, scope.MemberUserKey);
    }

    [Fact]
    public async Task Member_ShouldBeOutOfScope_FromAKurinTheyDoNotBelongTo()
    {
        await using var context = NewContext();
        var memberKey = Guid.NewGuid();
        var theirKurin = Guid.NewGuid();
        var anotherKurin = Guid.NewGuid();
        context.Memberships.Add(Joining(memberKey, theirKurin, null, Guid.NewGuid()));
        await context.SaveChangesAsync();

        var scope = await new ResourceScopeReader(context)
            .GetScopeAsync(ResourceType.Member, memberKey, anotherKurin);

        Assert.Null(scope);
    }

    [Fact]
    public async Task Member_ShouldFallOutOfScope_OnceTheMembershipEnds()
    {
        await using var context = NewContext();
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var left = Joining(memberKey, kurinKey, null, Guid.NewGuid());
        left.LeftAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Memberships.Add(left);
        await context.SaveChangesAsync();

        var scope = await new ResourceScopeReader(context)
            .GetScopeAsync(ResourceType.Member, memberKey, kurinKey);

        Assert.Null(scope);
    }

    [Fact]
    public async Task Member_InTwoKurins_ShouldResolveToTheGroupOfTheAskingOne()
    {
        await using var context = NewContext();
        var memberKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        var youthKurin = Guid.NewGuid();
        var youthGroup = Guid.NewGuid();
        var seniorKurin = Guid.NewGuid();

        context.Memberships.AddRange(
            Joining(memberKey, youthKurin, youthGroup, userKey),
            Joining(memberKey, seniorKurin, null, userKey));
        await context.SaveChangesAsync();

        var reader = new ResourceScopeReader(context);

        var fromYouth = await reader.GetScopeAsync(ResourceType.Member, memberKey, youthKurin);
        var fromSenior = await reader.GetScopeAsync(ResourceType.Member, memberKey, seniorKurin);

        Assert.Equal(youthGroup, fromYouth!.GroupKey);
        Assert.Null(fromSenior!.GroupKey);
    }

    /// <summary>
    /// Released from their last kurin, a person is in nobody's scope — and still known as the owner
    /// of their own record, by the closed membership that names their account.
    /// </summary>
    [Fact]
    public async Task PersonAccount_ShouldStayKnown_AfterTheLastMembershipEnds()
    {
        await using var context = NewContext();
        var memberKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        var left = Joining(memberKey, Guid.NewGuid(), null, userKey);
        left.LeftAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Memberships.Add(left);
        await context.SaveChangesAsync();

        var reader = new ResourceScopeReader(context);

        Assert.Equal(userKey, await reader.GetPersonAccountKeyAsync(ResourceType.Member, memberKey));
        Assert.Equal(userKey, await reader.GetPersonAccountKeyAsync(ResourceType.MemberContacts, memberKey));
        Assert.Null(await reader.GetPersonAccountKeyAsync(ResourceType.Member, Guid.NewGuid()));
        Assert.Null(await reader.GetPersonAccountKeyAsync(ResourceType.Kurin, memberKey));
    }

    [Fact]
    public async Task LedGroups_ShouldBeEmpty_InAKurinTheUserOnlyLeadsGroupsElsewhere()
    {
        await using var context = NewContext();
        var userKey = Guid.NewGuid();
        var ledKurin = Guid.NewGuid();
        var otherKurin = Guid.NewGuid();
        context.Memberships.Add(Joining(Guid.NewGuid(), ledKurin, Guid.NewGuid(), userKey));
        await context.SaveChangesAsync();

        var ledElsewhere = await new ResourceScopeReader(context)
            .GetLedGroupKeysAsync(userKey, otherKurin);

        Assert.Empty(ledElsewhere);
    }

    /// <summary>
    /// The journal reaches every гурток of its kurin, so an own-group reader passes the door and the
    /// read narrows the entries; a kurin-only scope would refuse him outright.
    /// </summary>
    [Fact]
    public async Task Journal_ShouldReachEveryGroupOfItsKurin()
    {
        await using var context = NewContext();
        var kurin = new Kurin(44);
        var first = new Group("Орли", kurin.KurinKey);
        var second = new Group("Сови", kurin.KurinKey);
        context.Kurins.Add(kurin);
        context.Groups.AddRange(first, second, new Group("Чужі", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var reader = new ResourceScopeReader(context);
        var scope = await reader.GetScopeAsync(ResourceType.KurinJournal, kurin.KurinKey, kurin.KurinKey);
        var missing = await reader.GetScopeAsync(ResourceType.KurinJournal, Guid.NewGuid(), kurin.KurinKey);

        Assert.NotNull(scope);
        Assert.Equal(kurin.KurinKey, scope!.KurinKey);
        Assert.Null(scope.GroupKey);
        Assert.Equal(new[] { first.GroupKey, second.GroupKey }.OrderBy(k => k), scope.GroupKeys!.OrderBy(k => k));
        Assert.Null(missing);
    }
}

/// <summary>
/// Whether a person's youth record is closed (LIFE-01): they stand in a УСП or УПС kurin now. A
/// senior kurin they left, or an УПЮ kurin they serve in as staff, does not make them one.
/// </summary>
public class ResourceScopeReaderSeniorTests
{
    private readonly AppDbContext _context = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private Guid KurinIn(KurinBranch branch)
    {
        var kurinKey = Guid.NewGuid();
        _context.Set<KurinBranchNumber>().Add(new KurinBranchNumber { KurinKey = kurinKey, Branch = branch, Number = 5 });
        return kurinKey;
    }

    private void Join(Guid memberKey, Guid kurinKey, bool left = false) =>
        _context.Memberships.Add(new Membership
        {
            MemberKey = memberKey,
            KurinKey = kurinKey,
            JoinedAtUtc = DateTime.UtcNow.AddYears(-3),
            LeftAtUtc = left ? DateTime.UtcNow.AddYears(-1) : null
        });

    [Fact]
    public async Task OnlyAStandingSeniorMembership_ClosesTheYouthRecord()
    {
        var senior = Guid.NewGuid();
        var formerSenior = Guid.NewGuid();
        var staffInUpyu = Guid.NewGuid();
        var upyu = KurinIn(KurinBranch.UPYu);
        Join(senior, upyu, left: true);
        Join(senior, KurinIn(KurinBranch.UPS));
        Join(formerSenior, KurinIn(KurinBranch.USP), left: true);
        Join(formerSenior, upyu);
        Join(staffInUpyu, upyu);
        await _context.SaveChangesAsync();

        var reader = new ResourceScopeReader(_context);

        Assert.True(await reader.StandsInSeniorKurinAsync(ResourceType.Member, senior));
        Assert.False(await reader.StandsInSeniorKurinAsync(ResourceType.Member, formerSenior));
        Assert.False(await reader.StandsInSeniorKurinAsync(ResourceType.Member, staffInUpyu));
    }
}

/// <summary>The owner's rule on two memberships (LIFE-01): while a юнак in an УПЮ kurin, they are a юнак.</summary>
public class ResourceScopeReaderTwoMembershipsTests
{
    [Fact]
    public async Task AnOpenYouthMembershipInAnUpyuKurin_KeepsTheRecordOpen_EvenBesideASeniorOne()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var upyu = Guid.NewGuid();
        var senior = Guid.NewGuid();
        context.Set<KurinBranchNumber>().AddRange(
            new KurinBranchNumber { KurinKey = upyu, Branch = KurinBranch.UPYu, Number = 1 },
            new KurinBranchNumber { KurinKey = senior, Branch = KurinBranch.USP, Number = 5 });
        var youth = Guid.NewGuid();
        var staff = Guid.NewGuid();
        foreach (var (member, kurin, kind) in new[]
                 {
                     (youth, upyu, MembershipKind.Youth), (youth, senior, MembershipKind.Youth),
                     (staff, upyu, MembershipKind.Staff), (staff, senior, MembershipKind.Youth)
                 })
        {
            context.Memberships.Add(new Membership { MemberKey = member, KurinKey = kurin, Kind = kind, JoinedAtUtc = DateTime.UtcNow.AddYears(-1) });
        }

        await context.SaveChangesAsync();
        var reader = new ResourceScopeReader(context);

        Assert.False(await reader.StandsInSeniorKurinAsync(ResourceType.Member, youth));
        Assert.True(await reader.StandsInSeniorKurinAsync(ResourceType.Member, staff));
    }
}
