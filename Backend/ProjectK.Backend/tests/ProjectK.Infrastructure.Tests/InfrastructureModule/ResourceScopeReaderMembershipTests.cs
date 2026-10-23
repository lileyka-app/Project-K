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
