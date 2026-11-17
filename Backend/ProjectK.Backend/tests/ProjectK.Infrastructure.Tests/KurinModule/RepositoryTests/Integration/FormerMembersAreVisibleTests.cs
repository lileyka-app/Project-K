using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.KurinModule;
using ProjectK.Infrastructure.Tests.MemberModule.RepositoryTests.Integration;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>
/// Removing someone from a kurin used to put them out of the провід's reach entirely: every
/// membership read filters closed rows out, so they were gone from every screen and could only be
/// taken back with the public code they themselves hold. The row was always there — this is the read
/// that lets a kurin see its own past.
/// </summary>
public sealed class FormerMembersAreVisibleTests
{
    private static AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Member Person(string lastName) => new()
    {
        MemberKey = Guid.NewGuid(),
        FirstName = "Тест",
        LastName = lastName,
        Email = $"{lastName}@example.com",
        PhoneNumber = "0500000000",
        DateOfBirth = new DateOnly(2008, 1, 1)
    };

    private static Membership Spell(Member person, Guid kurinKey, Guid? groupKey, DateTime joined, DateTime? left)
        => new()
        {
            MemberKey = person.MemberKey,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            JoinedAtUtc = joined,
            LeftAtUtc = left
        };

    [Fact]
    public async Task ShouldListWhoLeft_WithTheГурток_TheyWereIn()
    {
        using var context = NewContext();
        var kurin = new Kurin(41);
        var alpha = new Group("Alpha", kurin.KurinKey);
        var stayed = Person("Лишилась");
        var left = Person("Пішов");

        context.AddRange(kurin, alpha, stayed, left);
        context.AddRange(
            Spell(stayed, kurin.KurinKey, alpha.GroupKey, DateTime.UtcNow.AddYears(-2), null),
            Spell(left, kurin.KurinKey, alpha.GroupKey, DateTime.UtcNow.AddYears(-2), DateTime.UtcNow.AddDays(-3)));
        await context.SaveChangesAsync();

        var former = await new MembershipRepository(context).GetFormerInKurinAsync(kurin.KurinKey);

        var person = Assert.Single(former);
        Assert.Equal(left.MemberKey, person.MemberKey);
        Assert.Equal("Пішов", person.LastName);
        Assert.Equal("Alpha", person.GroupName);
    }

    /// <summary>
    /// Someone taken back is not former, however many closed spells they have behind them. Getting
    /// this wrong would show the same person in both tables at once.
    /// </summary>
    [Fact]
    public async Task ShouldNotListSomeoneWhoWasTakenBack()
    {
        using var context = NewContext();
        var kurin = new Kurin(42);
        var returned = Person("Повернувся");

        context.AddRange(kurin, returned);
        context.AddRange(
            Spell(returned, kurin.KurinKey, null, DateTime.UtcNow.AddYears(-3), DateTime.UtcNow.AddYears(-1)),
            Spell(returned, kurin.KurinKey, null, DateTime.UtcNow.AddMonths(-2), null));
        await context.SaveChangesAsync();

        var former = await new MembershipRepository(context).GetFormerInKurinAsync(kurin.KurinKey);

        Assert.Empty(former);
    }

    /// <summary>Left twice, so the row that matters is the later departure — one entry, not two.</summary>
    [Fact]
    public async Task ShouldReportTheLastSpell_Once()
    {
        using var context = NewContext();
        var kurin = new Kurin(43);
        var alpha = new Group("Alpha", kurin.KurinKey);
        var beta = new Group("Beta", kurin.KurinKey);
        var person = Person("Двічі");
        var lastLeft = DateTime.UtcNow.AddDays(-5);

        context.AddRange(kurin, alpha, beta, person);
        context.AddRange(
            Spell(person, kurin.KurinKey, alpha.GroupKey, DateTime.UtcNow.AddYears(-4), DateTime.UtcNow.AddYears(-3)),
            Spell(person, kurin.KurinKey, beta.GroupKey, DateTime.UtcNow.AddYears(-1), lastLeft));
        await context.SaveChangesAsync();

        var former = await new MembershipRepository(context).GetFormerInKurinAsync(kurin.KurinKey);

        var entry = Assert.Single(former);
        Assert.Equal("Beta", entry.GroupName);
        Assert.Equal(lastLeft, entry.LeftAtUtc);
    }

    /// <summary>
    /// A kurin is owed its own history and no one else's. Someone who left a different kurin has
    /// nothing to do with this one, even though the person record is shared.
    /// </summary>
    [Fact]
    public async Task ShouldNotReachIntoAnotherKurinsPast()
    {
        using var context = NewContext();
        var here = new Kurin(44);
        var elsewhere = new Kurin(45);
        var person = Person("Чужа");

        context.AddRange(here, elsewhere, person);
        context.Add(Spell(person, elsewhere.KurinKey, null, DateTime.UtcNow.AddYears(-2), DateTime.UtcNow.AddDays(-1)));
        await context.SaveChangesAsync();

        var former = await new MembershipRepository(context).GetFormerInKurinAsync(here.KurinKey);

        Assert.Empty(former);
    }
}
