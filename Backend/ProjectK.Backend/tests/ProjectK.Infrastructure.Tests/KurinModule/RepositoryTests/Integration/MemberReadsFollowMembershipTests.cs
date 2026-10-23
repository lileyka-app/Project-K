using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;
using Xunit;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>
/// Who a kurin''s people are is answered by its memberships, and by nothing else — a person''s own
/// record no longer says where they are at all. These read the lists from both sides of that.
/// </summary>
public class MemberReadsFollowMembershipTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Member Person(string firstName = "Оксана") => new()
    {
        MemberKey = Guid.NewGuid(),
        FirstName = firstName,
        LastName = "Тестова",
        Email = $"{Guid.NewGuid():N}@example.com",
        PhoneNumber = "0500000000",
        DateOfBirth = new DateOnly(2005, 4, 1)
    };

    private static Membership Joining(Guid memberKey, Guid kurinKey, Guid? groupKey = null) => new()
    {
        MemberKey = memberKey,
        KurinKey = kurinKey,
        GroupKey = groupKey,
        Kind = MembershipKind.Youth,
        JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
    };

    [Fact]
    public async Task KurinList_ShouldPlaceEveryoneByTheirOwnMembershipsOwnGroup()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var onlyGroup = Guid.NewGuid();
        var person = Person();
        context.Members.Add(person);
        context.Memberships.Add(Joining(person.MemberKey, kurinKey, onlyGroup));
        await context.SaveChangesAsync();

        var listed = Assert.Single(await uow.Members.GetListItemsByKurinKeyAsync(kurinKey));

        Assert.Equal(onlyGroup, listed.GroupKey);
        Assert.Equal(kurinKey, listed.KurinKey);
    }

    [Fact]
    public async Task SomeoneWhoLeft_ShouldNotBeInTheKurinsListAnyMore()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var person = Person();
        var left = Joining(person.MemberKey, kurinKey);
        left.LeftAtUtc = DateTime.UtcNow.AddDays(-2);
        context.Members.Add(person);
        context.Memberships.Add(left);
        await context.SaveChangesAsync();

        Assert.Empty(await uow.Members.GetListItemsByKurinKeyAsync(kurinKey));
        Assert.Empty(await uow.Members.GetSummariesByKurinKeyAsync(kurinKey));
        Assert.Null(await uow.Members.GetKurinKeyByMemberAsync(person.MemberKey));
    }

    [Fact]
    public async Task SomeoneInTwoKurins_ShouldBeListedInBoth_EachWithThatKurinsPlacement()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var youthKurin = Guid.NewGuid();
        var youthGroup = Guid.NewGuid();
        var seniorKurin = Guid.NewGuid();
        var person = Person();

        context.Members.Add(person);
        context.Memberships.AddRange(
            Joining(person.MemberKey, youthKurin, youthGroup),
            Joining(person.MemberKey, seniorKurin));
        await context.SaveChangesAsync();

        var inYouth = Assert.Single(await uow.Members.GetListItemsByKurinKeyAsync(youthKurin));
        var inSenior = Assert.Single(await uow.Members.GetListItemsByKurinKeyAsync(seniorKurin));

        Assert.Equal(person.MemberKey, inYouth.MemberKey);
        Assert.Equal(person.MemberKey, inSenior.MemberKey);
        Assert.Equal(youthGroup, inYouth.GroupKey);
        Assert.Null(inSenior.GroupKey);
    }

    /// <summary>
    /// What the agenda reads instead of the whole kurin: the people named, everyone in the гуртки
    /// named, the people behind the accounts named — each placed by this kurin's membership, and
    /// nobody from another kurin even when their key is asked for.
    /// </summary>
    [Fact]
    public async Task ASelection_ShouldAnswerWithTheMembersGroupsAndAccountsNamed_InThisKurinOnly()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var otherKurin = Guid.NewGuid();
        var group = Guid.NewGuid();
        var byKey = Person("Названа");
        var inGroup = Person("Угуртку");
        var byAccount = Person("Заакаунтом");
        byAccount.UserKey = Guid.NewGuid();
        var bystander = Person("Стороння");
        var elsewhere = Person("Вінших");
        context.Members.AddRange(byKey, inGroup, byAccount, bystander, elsewhere);
        context.Memberships.AddRange(
            Joining(byKey.MemberKey, kurinKey),
            Joining(inGroup.MemberKey, kurinKey, group),
            Joining(byAccount.MemberKey, kurinKey),
            Joining(bystander.MemberKey, kurinKey, Guid.NewGuid()),
            Joining(elsewhere.MemberKey, otherKurin, group));
        await context.SaveChangesAsync();

        var selection = new MemberSelection([byKey.MemberKey, elsewhere.MemberKey], [group], [byAccount.UserKey!.Value]);
        var found = await uow.Members.GetSummariesByKurinKeyAsync(kurinKey, selection);

        Assert.Equal(
            new[] { byAccount.MemberKey, byKey.MemberKey, inGroup.MemberKey }.OrderBy(k => k),
            found.Select(f => f.MemberKey).OrderBy(k => k));
        Assert.All(found, f => Assert.Equal(kurinKey, f.KurinKey));
        Assert.Equal(group, found.Single(f => f.MemberKey == inGroup.MemberKey).GroupKey);
        Assert.Empty(await uow.Members.GetSummariesByKurinKeyAsync(kurinKey, MemberSelection.None));
    }

    [Fact]
    public async Task SomeoneWhoBelongsNowhere_ShouldStillBeFound_WithNoKurin()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var person = Person();
        context.Members.Add(person);
        await context.SaveChangesAsync();

        var summary = await uow.Members.GetSummaryByKeyAsync(person.MemberKey);

        Assert.NotNull(summary);
        Assert.Equal(person.MemberKey, summary!.MemberKey);
        Assert.Equal(Guid.Empty, summary.KurinKey);
        Assert.Null(summary.GroupKey);
    }

    [Fact]
    public async Task Placing_SomeoneInAnotherKurin_ShouldCloseTheOneTheyWereIn()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var wasIn = Guid.NewGuid();
        var movesTo = Guid.NewGuid();
        var newGroup = Guid.NewGuid();
        var person = Person();
        context.Members.Add(person);
        context.Memberships.Add(Joining(person.MemberKey, wasIn));
        await context.SaveChangesAsync();

        await uow.Memberships.PlaceAsync(person.MemberKey, null, movesTo, newGroup);
        await uow.SaveChangesAsync();

        var current = await uow.Memberships.GetActiveForMemberAsync(person.MemberKey);
        var only = Assert.Single(current);
        Assert.Equal(movesTo, only.KurinKey);
        Assert.Equal(newGroup, only.GroupKey);

        var closed = context.Memberships.Single(ms => ms.KurinKey == wasIn);
        Assert.NotNull(closed.LeftAtUtc);
    }

    [Fact]
    public async Task Placing_SomeoneWhereTheyAlreadyAre_ShouldMoveTheirGroup_WithoutOpeningASecond()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        var account = Guid.NewGuid();
        var person = Person();
        context.Members.Add(person);
        context.Memberships.Add(Joining(person.MemberKey, kurinKey, from));
        await context.SaveChangesAsync();

        await uow.Memberships.PlaceAsync(person.MemberKey, account, kurinKey, to);
        await uow.SaveChangesAsync();

        var only = Assert.Single(await uow.Memberships.GetActiveForMemberAsync(person.MemberKey));
        Assert.Equal(to, only.GroupKey);
        Assert.Equal(account, only.UserKey);
        Assert.Single(context.Memberships.Where(ms => ms.MemberKey == person.MemberKey));
    }

    [Fact]
    public async Task Placing_APersonJustCreated_ShouldNotOpenTwoMemberships()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var person = Person();
        uow.Members.Create(person);

        // Both of these land before anything is saved — the second must find what the first opened.
        await uow.Memberships.PlaceAsync(person.MemberKey, null, kurinKey, null);
        await uow.Memberships.PlaceAsync(person.MemberKey, null, kurinKey, null);
        await uow.SaveChangesAsync();

        Assert.Single(context.Memberships.Where(ms => ms.MemberKey == person.MemberKey));
    }

    [Fact]
    public async Task RemovingPeople_ShouldTakeTheirMembershipsWithThem()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var kurinKey = Guid.NewGuid();
        var goes = Person();
        var stays = Person("Марта");
        context.Members.AddRange(goes, stays);
        context.Memberships.AddRange(
            Joining(goes.MemberKey, kurinKey),
            Joining(stays.MemberKey, kurinKey));
        await context.SaveChangesAsync();

        await uow.Memberships.RemoveForMembersAsync([goes.MemberKey]);
        await uow.SaveChangesAsync();

        var left = Assert.Single(context.Memberships);
        Assert.Equal(stays.MemberKey, left.MemberKey);
    }
}
