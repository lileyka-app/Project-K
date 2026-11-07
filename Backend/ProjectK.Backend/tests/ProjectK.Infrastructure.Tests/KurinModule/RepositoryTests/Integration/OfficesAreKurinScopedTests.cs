using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.KurinModule;
using Xunit;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>
/// The whole of v0.20.0 in one claim: an office is held in a kurin, not in the system. A виховник of
/// one kurin who also belongs to another is an ordinary member there, and every test here sets up
/// exactly that person and asks each kurin in turn.
/// </summary>
public class OfficesAreKurinScopedTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly MemberOffice Vykhovnyk = new(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    private static readonly MemberOffice Zvyazkovyi = new(LeadershipType.KV, LeadershipRole.Zvyazkovyi);

    private sealed record Person(Guid MemberKey, Guid UserKey);

    private static Person Belonging(AppDbContext context, Guid kurinKey, Guid? groupKey = null, Person? who = null)
    {
        var person = who ?? new Person(Guid.NewGuid(), Guid.NewGuid());
        context.Memberships.Add(new Membership
        {
            MemberKey = person.MemberKey,
            UserKey = person.UserKey,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            Kind = MembershipKind.Youth,
            JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
        });
        return person;
    }

    private static void Seats(
        AppDbContext context,
        Person person,
        LeadershipType type,
        LeadershipRole role,
        Guid? kurinKey = null,
        Guid? groupKey = null)
    {
        var office = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = type,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
        };
        context.Leaderships.Add(office);
        context.LeadershipHistories.Add(new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            MemberKey = person.MemberKey,
            LeadershipKey = office.LeadershipKey,
            Role = role,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
        });
    }

    [Fact]
    public async Task AVykhovnykOfOneKurin_ShouldBeAnOrdinaryMemberOfTheOther()
    {
        await using var context = NewContext();
        var whereTheyLead = Guid.NewGuid();
        var whereTheyJustBelong = Guid.NewGuid();

        var person = Belonging(context, whereTheyLead);
        Belonging(context, whereTheyJustBelong, who: person);
        Seats(context, person, LeadershipType.KV, LeadershipRole.Vykhovnyk, kurinKey: whereTheyLead);
        await context.SaveChangesAsync();

        var repository = new LeadershipRepository(context);

        var here = await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, whereTheyLead);
        var there = await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, whereTheyJustBelong);

        Assert.Equal(Vykhovnyk, Assert.Single(here));
        Assert.Empty(there);
    }

    [Fact]
    public async Task AGroupOffice_ShouldCountTowardsTheKurinThatGroupBelongsTo()
    {
        await using var context = NewContext();
        var kurinKey = Guid.NewGuid();
        var otherKurin = Guid.NewGuid();
        var group = new Group("Ведмеді", kurinKey) { GroupKey = Guid.NewGuid() };
        context.Kurins.Add(new Kurin(1) { KurinKey = kurinKey });
        context.Groups.Add(group);

        var person = Belonging(context, kurinKey, group.GroupKey);
        Belonging(context, otherKurin, who: person);
        Seats(context, person, LeadershipType.KV, LeadershipRole.Vykhovnyk, groupKey: group.GroupKey);
        await context.SaveChangesAsync();

        var repository = new LeadershipRepository(context);

        Assert.Equal(
            Vykhovnyk,
            Assert.Single(await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey)));
        Assert.Empty(await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, otherKurin));
    }

    [Fact]
    public async Task SomeoneWhoLeftTheKurin_ShouldHoldNoOfficeInIt()
    {
        await using var context = NewContext();
        var kurinKey = Guid.NewGuid();
        var person = new Person(Guid.NewGuid(), Guid.NewGuid());

        Belonging(context, kurinKey, who: person);
        Seats(context, person, LeadershipType.KV, LeadershipRole.Zvyazkovyi, kurinKey: kurinKey);
        await context.SaveChangesAsync();

        var membership = context.Memberships.Single(ms => ms.MemberKey == person.MemberKey);
        membership.LeftAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Assert.Empty(await new LeadershipRepository(context)
            .GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey));
    }

    [Fact]
    public async Task AnEndedTermInOffice_ShouldNotCount()
    {
        await using var context = NewContext();
        var kurinKey = Guid.NewGuid();
        var person = Belonging(context, kurinKey);
        Seats(context, person, LeadershipType.KV, LeadershipRole.Zvyazkovyi, kurinKey: kurinKey);
        await context.SaveChangesAsync();

        var stillHolds = await new LeadershipRepository(context)
            .GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey);
        Assert.Equal(Zvyazkovyi, Assert.Single(stillHolds));

        context.LeadershipHistories.Single().EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await context.SaveChangesAsync();

        Assert.Empty(await new LeadershipRepository(context)
            .GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey));
    }

    [Fact]
    public async Task AMentorAssignment_ShouldMakeSomeoneAVykhovnyk_OnlyInThatGroupsKurin()
    {
        await using var context = NewContext();
        var mentorsKurin = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var group = new Group("Соколи", mentorsKurin) { GroupKey = Guid.NewGuid() };
        context.Kurins.Add(new Kurin(2) { KurinKey = mentorsKurin });
        context.Groups.Add(group);

        var person = Belonging(context, mentorsKurin);
        Belonging(context, elsewhere, who: person);
        context.MentorAssignments.Add(new MentorAssignment
        {
            MentorAssignmentKey = Guid.NewGuid(),
            MentorUserKey = person.UserKey,
            GroupKey = group.GroupKey,
            AssignedAtUtc = DateTime.UtcNow.AddMonths(-1)
        });
        await context.SaveChangesAsync();

        var repository = new LeadershipRepository(context);

        Assert.Equal(
            Vykhovnyk,
            Assert.Single(await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, mentorsKurin)));
        Assert.Empty(await repository.GetActiveOfficesForAccountInKurinAsync(person.UserKey, elsewhere));
    }

    [Fact]
    public async Task ARevokedMentorAssignment_ShouldGrantNothing()
    {
        await using var context = NewContext();
        var kurinKey = Guid.NewGuid();
        var group = new Group("Соколи", kurinKey) { GroupKey = Guid.NewGuid() };
        context.Kurins.Add(new Kurin(3) { KurinKey = kurinKey });
        context.Groups.Add(group);

        var person = Belonging(context, kurinKey);
        context.MentorAssignments.Add(new MentorAssignment
        {
            MentorAssignmentKey = Guid.NewGuid(),
            MentorUserKey = person.UserKey,
            GroupKey = group.GroupKey,
            AssignedAtUtc = DateTime.UtcNow.AddMonths(-1),
            RevokedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        Assert.Empty(await new LeadershipRepository(context)
            .GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey));
    }

    [Fact]
    public async Task AnAccountWithNoMembershipHere_ShouldHoldNoOffice_EvenIfSeatedInOne()
    {
        await using var context = NewContext();
        var kurinKey = Guid.NewGuid();
        var person = new Person(Guid.NewGuid(), Guid.NewGuid());

        // Seated in the office but never made a member of the kurin — the office alone is not reach.
        Seats(context, person, LeadershipType.KV, LeadershipRole.Zvyazkovyi, kurinKey: kurinKey);
        await context.SaveChangesAsync();

        Assert.Empty(await new LeadershipRepository(context)
            .GetActiveOfficesForAccountInKurinAsync(person.UserKey, kurinKey));
    }

    [Fact]
    public async Task TheKurinsMemberList_ShouldShowOnlyOfficesHeldInThatKurin()
    {
        await using var context = NewContext();
        var whereTheyLead = Guid.NewGuid();
        var whereTheyJustBelong = Guid.NewGuid();

        var person = Belonging(context, whereTheyLead);
        Belonging(context, whereTheyJustBelong, who: person);
        context.Members.Add(new Member { MemberKey = person.MemberKey, UserKey = person.UserKey, FirstName = "Ірина", LastName = "Федорів", Email = "iryna@projectk.com", PhoneNumber = "0500000000" });
        Seats(context, person, LeadershipType.Kurin, LeadershipRole.Pysar, kurinKey: whereTheyLead);
        await context.SaveChangesAsync();

        var repository = new MemberRepository(context);

        var here = Assert.Single(Assert.Single(await repository.GetListItemsByKurinKeyAsync(whereTheyLead)).LeadershipHistories);
        Assert.Equal(whereTheyLead, here.KurinKey);
        Assert.Empty(Assert.Single(await repository.GetListItemsByKurinKeyAsync(whereTheyJustBelong)).LeadershipHistories);
    }
}
