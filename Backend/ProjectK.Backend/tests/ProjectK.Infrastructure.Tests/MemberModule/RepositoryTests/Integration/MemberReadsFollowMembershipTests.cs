using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.MemberModule.RepositoryTests.Integration;

/// <summary>
/// Who a kurin''s people are is answered by its memberships, and by nothing else — a person''s own
/// record no longer says where they are at all. These read the lists from both sides of that.
/// </summary>
public class MemberReadsFollowMembershipTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
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

        var listed = Assert.Single(await uow.Memberships.GetRosterRowsByKurinAsync(kurinKey));

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

        Assert.Empty(await uow.Memberships.GetRosterRowsByKurinAsync(kurinKey));
        Assert.Empty(await uow.Memberships.GetPeopleInKurinAsync(kurinKey));
        Assert.Null(await uow.Memberships.FindInLatestKurinAsync(person.MemberKey));
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

        var inYouth = Assert.Single(await uow.Memberships.GetRosterRowsByKurinAsync(youthKurin));
        var inSenior = Assert.Single(await uow.Memberships.GetRosterRowsByKurinAsync(seniorKurin));

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
        var found = await uow.Memberships.GetPeopleInKurinAsync(kurinKey, selection);

        Assert.Equal(
            new[] { byAccount.MemberKey, byKey.MemberKey, inGroup.MemberKey }.OrderBy(k => k),
            found.Select(f => f.MemberKey).OrderBy(k => k));
        Assert.All(found, f => Assert.Equal(kurinKey, f.KurinKey));
        Assert.Equal(group, found.Single(f => f.MemberKey == inGroup.MemberKey).GroupKey);
        Assert.Empty(await uow.Memberships.GetPeopleInKurinAsync(kurinKey, MemberSelection.None));
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
        Assert.Null(await uow.Memberships.FindInLatestKurinAsync(person.MemberKey));
    }

    [Fact]
    public async Task SomeoneInTwoKurins_ShouldStandForTheOneJoinedLast_WhereOneKurinHasTo()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var first = Guid.NewGuid();
        var latest = Guid.NewGuid();
        var latestGroup = Guid.NewGuid();
        var person = Person();
        person.UserKey = Guid.NewGuid();
        var joinedLater = Joining(person.MemberKey, latest, latestGroup);
        joinedLater.JoinedAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Members.Add(person);
        context.Memberships.AddRange(Joining(person.MemberKey, first), joinedLater);
        await context.SaveChangesAsync();

        var byMember = await uow.Memberships.FindInLatestKurinAsync(person.MemberKey);
        var byAccount = await uow.Memberships.FindInLatestKurinByAccountAsync(person.UserKey.Value);

        Assert.Equal((latest, (Guid?)latestGroup), (byMember!.KurinKey, byMember.GroupKey));
        Assert.Equal(byMember, byAccount);
        Assert.Equal(person.FirstName, byMember.Person.FirstName);
    }

    [Fact]
    public async Task SomeoneInTwoKurins_ShouldBePlacedByTheMembershipOfTheKurinAskedAbout()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var first = Guid.NewGuid();
        var firstGroup = Guid.NewGuid();
        var latest = Guid.NewGuid();
        var person = Person();
        person.UserKey = Guid.NewGuid();
        var joinedLater = Joining(person.MemberKey, latest);
        joinedLater.JoinedAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Members.Add(person);
        context.Memberships.AddRange(Joining(person.MemberKey, first, firstGroup), joinedLater);
        await context.SaveChangesAsync();

        var inFirst = await uow.Memberships.FindInKurinByAccountAsync(person.UserKey.Value, first);

        Assert.Equal((first, (Guid?)firstGroup), (inFirst!.KurinKey, inFirst.GroupKey));
        Assert.Equal(person.MemberKey, inFirst.MemberKey);
        Assert.Null(await uow.Memberships.FindInKurinByAccountAsync(person.UserKey.Value, Guid.NewGuid()));
    }

    [Fact]
    public async Task Placing_SomeoneInAnotherKurin_ShouldKeepTheOneTheyWereIn()
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
        Assert.Equal(2, current.Count);
        Assert.Equal(newGroup, current.Single(ms => ms.KurinKey == movesTo).GroupKey);
        Assert.Null(context.Memberships.Single(ms => ms.KurinKey == wasIn).LeftAtUtc);
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
    public async Task TheYouthKurin_IsTheNewestUpyuOne_EvenWhenASeniorOneWasJoinedLater()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var olderYouth = new Kurin(11);
        var newerYouth = new Kurin(12);
        var senior = new Kurin(5, KurinBranch.USP);
        var person = Person();
        var inOlder = Joining(person.MemberKey, olderYouth.KurinKey);
        inOlder.JoinedAtUtc = DateTime.UtcNow.AddYears(-3);
        var inNewer = Joining(person.MemberKey, newerYouth.KurinKey, Guid.NewGuid());
        inNewer.JoinedAtUtc = DateTime.UtcNow.AddYears(-2);
        var inSenior = Joining(person.MemberKey, senior.KurinKey);
        inSenior.JoinedAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Kurins.AddRange(olderYouth, newerYouth, senior);
        context.Members.Add(person);
        context.Memberships.AddRange(inOlder, inNewer, inSenior);
        await context.SaveChangesAsync();

        var youth = await uow.Memberships.FindInYouthKurinAsync(person.MemberKey);

        Assert.Equal((newerYouth.KurinKey, inNewer.GroupKey), (youth!.KurinKey, youth.GroupKey));
        Assert.Equal(senior.KurinKey, (await uow.Memberships.FindInLatestKurinAsync(person.MemberKey))!.KurinKey);
    }

    [Fact]
    public async Task TheYouthKurin_OfSomeoneInNoUpyuKurin_IsTheNewestOneAtAll()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var usp = new Kurin(5, KurinBranch.USP);
        var ups = new Kurin(20, KurinBranch.UPS);
        var person = Person();
        var inUps = Joining(person.MemberKey, ups.KurinKey);
        inUps.JoinedAtUtc = DateTime.UtcNow.AddDays(-1);
        context.Kurins.AddRange(usp, ups);
        context.Members.Add(person);
        context.Memberships.AddRange(Joining(person.MemberKey, usp.KurinKey), inUps);
        await context.SaveChangesAsync();

        Assert.Equal(ups.KurinKey, (await uow.Memberships.FindInYouthKurinAsync(person.MemberKey))!.KurinKey);
        Assert.Null(await uow.Memberships.FindInYouthKurinAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task WaitingLevels_AreReadForManyPeopleAtOnce_AndOnlyTheWaitingOnes()
    {
        await using var context = NewContext();
        var uow = new InfraUnitOfWork(context);

        var asked = Person();
        var other = Person("Марта");
        context.Members.AddRange(asked, other);
        context.PlastLevelHistories.AddRange(
            new PlastLevelHistory { MemberKey = asked.MemberKey, PlastLevel = PlastLevel.Skob },
            new PlastLevelHistory { MemberKey = asked.MemberKey, PlastLevel = PlastLevel.Starshoplastun, AwaitsConfirmation = true },
            new PlastLevelHistory { MemberKey = other.MemberKey, PlastLevel = PlastLevel.Senior, AwaitsConfirmation = true });
        await context.SaveChangesAsync();

        var waiting = Assert.Single(await uow.Members.GetWaitingLevelsAsync([asked.MemberKey]));

        Assert.Equal((asked.MemberKey, PlastLevel.Starshoplastun), (waiting.MemberKey, waiting.PlastLevel));
        Assert.Empty(await uow.Members.GetWaitingLevelsAsync([]));
    }

    [Fact]
    public async Task ALevelSavedWithoutAWord_CountsAtOnce()
    {
        await using var context = NewContext();

        var person = Person();
        context.Members.Add(person);
        context.PlastLevelHistories.Add(new PlastLevelHistory { MemberKey = person.MemberKey, PlastLevel = PlastLevel.Senior });
        await context.SaveChangesAsync();

        var stored = await context.PlastLevelHistories.AsNoTracking().SingleAsync();
        Assert.False(stored.AwaitsConfirmation);
        Assert.Null(stored.ConfirmedByUserKey);
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
