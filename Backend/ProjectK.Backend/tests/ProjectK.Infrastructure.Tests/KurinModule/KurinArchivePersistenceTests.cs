using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.KurinModule;
using Xunit;

namespace ProjectK.Infrastructure.Tests.KurinModule;

/// <summary>
/// The archive's memory: the гуртки a membership passes through are recorded by the context on
/// every save, and a dissolved гурток is gone from ordinary reads but not from the archive.
/// </summary>
public class KurinArchivePersistenceTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _person = Guid.NewGuid();
    private readonly Group _sokoly;
    private readonly Group _vovky;
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    public KurinArchivePersistenceTests()
    {
        _sokoly = new Group("Соколи", _kurinKey);
        _vovky = new Group("Вовки", _kurinKey);
        using var context = new AppDbContext(_options);
        context.Groups.AddRange(_sokoly, _vovky);
        context.SaveChanges();
    }

    private Membership Join(AppDbContext context, Guid? groupKey)
    {
        var membership = new Membership { MemberKey = _person, KurinKey = _kurinKey, GroupKey = groupKey, JoinedAtUtc = new DateTime(2024, 9, 1) };
        context.Memberships.Add(membership);
        return membership;
    }

    [Fact]
    public async Task CorrectedDates_MoveTheEdgesOfTheStints()
    {
        var left = new DateTime(2026, 6, 30);
        Guid membershipKey;
        DateTime joined;
        await using (var context = new AppDbContext(_options))
        {
            var membership = Join(context, _sokoly.GroupKey);
            membership.LeftAtUtc = left;
            (membershipKey, joined) = (membership.MembershipKey, membership.JoinedAtUtc);
            await context.SaveChangesAsync();
        }

        var newLeft = new DateTime(2026, 5, 1);
        var newJoined = new DateTime(2024, 9, 1);
        await using (var context = new AppDbContext(_options))
        {
            await new KurinArchiveRepository(context).MoveStintEdgesAsync(membershipKey, joined, newJoined, left, newLeft);
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var stint = await read.MembershipGroupStints.SingleAsync();
        Assert.Equal(newJoined, stint.FromUtc);
        Assert.Equal(newLeft, stint.ToUtc);
    }

    [Fact]
    public async Task Joining_Moving_AndLeaving_LeaveOneStintPerGurtok()
    {
        Guid membershipKey;
        await using (var context = new AppDbContext(_options))
        {
            membershipKey = Join(context, _sokoly.GroupKey).MembershipKey;
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            var membership = await context.Memberships.SingleAsync();
            membership.GroupKey = _vovky.GroupKey;
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            var membership = await context.Memberships.SingleAsync();
            membership.LeftAtUtc = new DateTime(2026, 6, 30);
            context.SaveChanges();
        }

        await using var read = new AppDbContext(_options);
        var stints = await read.MembershipGroupStints.Where(stint => stint.MembershipKey == membershipKey).OrderBy(stint => stint.FromUtc).ToListAsync();
        Assert.Equal([_sokoly.GroupKey, _vovky.GroupKey], stints.Select(stint => stint.GroupKey));
        Assert.Equal(new DateTime(2024, 9, 1), stints[0].FromUtc);
        Assert.NotNull(stints[0].ToUtc);
        Assert.Equal(new DateTime(2026, 6, 30), stints[1].ToUtc);
    }

    [Fact]
    public async Task AStretchWithNoRecordedStart_IsClosedAllTheSame()
    {
        // Memberships standing in a гурток before 1.4 got a stint with no start from the migration.
        await using (var context = new AppDbContext(_options))
        {
            Join(context, _sokoly.GroupKey);
            await context.SaveChangesAsync();
            (await context.MembershipGroupStints.SingleAsync()).FromUtc = null;
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            (await context.Memberships.SingleAsync()).GroupKey = null;
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var stint = await read.MembershipGroupStints.SingleAsync();
        Assert.Null(stint.FromUtc);
        Assert.NotNull(stint.ToUtc);
    }

    [Fact]
    public async Task ADissolvedGurtok_IsGoneFromReads_ButTheArchiveKeepsItAndItsProvid()
    {
        var holder = Guid.NewGuid();
        await using (var context = new AppDbContext(_options))
        {
            var group = await context.Groups.SingleAsync(g => g.GroupKey == _sokoly.GroupKey);
            group.DissolvedAtUtc = new DateTime(2026, 10, 1);
            context.Leaderships.Add(new Leadership
            {
                Type = LeadershipType.Group,
                GroupKey = group.GroupKey,
                StartDate = new DateOnly(2023, 9, 1),
                EndDate = new DateOnly(2026, 10, 1),
                LeadershipHistories =
                {
                    new LeadershipHistory { MemberKey = holder, Role = LeadershipRole.Hurtkoviy, StartDate = new DateOnly(2023, 9, 1), EndDate = new DateOnly(2026, 10, 1) }
                }
            });
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        Assert.Equal([_vovky.GroupKey], await read.Groups.Select(g => g.GroupKey).ToListAsync());

        var archive = new KurinArchiveRepository(read);
        Assert.Equal([_sokoly.GroupKey], (await archive.GetDissolvedGroupsAsync(_kurinKey)).Select(g => g.GroupKey));
        var office = Assert.Single(await archive.GetOfficesAsync(_kurinKey));
        Assert.Equal(holder, office.MemberKey);
        Assert.Equal("Соколи", office.GroupName);
        Assert.True(office.GroupDissolved);
    }

    [Fact]
    public async Task DeletingAMembership_TakesTheOfficesHeldInItsTime()
    {
        Guid membershipKey;
        await using (var context = new AppDbContext(_options))
        {
            var membership = Join(context, _sokoly.GroupKey);
            membership.LeftAtUtc = new DateTime(2025, 6, 30);
            membershipKey = membership.MembershipKey;
            context.Leaderships.Add(new Leadership
            {
                Type = LeadershipType.Kurin,
                KurinKey = _kurinKey,
                StartDate = new DateOnly(2020, 1, 1),
                LeadershipHistories =
                {
                    new LeadershipHistory { MemberKey = _person, Role = LeadershipRole.Pysar, StartDate = new DateOnly(2024, 10, 1), EndDate = new DateOnly(2025, 5, 1) },
                    new LeadershipHistory { MemberKey = _person, Role = LeadershipRole.Skarbnyk, StartDate = new DateOnly(2019, 1, 1), EndDate = new DateOnly(2020, 1, 1) }
                }
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            var archive = new KurinArchiveRepository(context);
            var membership = await archive.FindMembershipAsync(membershipKey);
            var removed = await archive.RemoveOfficesHeldAsync(_person, _kurinKey, new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));
            archive.RemoveMembership(membership!);
            await context.SaveChangesAsync();
            Assert.Equal([LeadershipRole.Pysar], removed.Select(history => history.Role));
        }

        await using var read = new AppDbContext(_options);
        Assert.Empty(await read.Memberships.ToListAsync());
        Assert.Equal([LeadershipRole.Skarbnyk], await read.LeadershipHistories.Select(history => history.Role).ToListAsync());
    }

    [Fact]
    public async Task AnOfficeStillHeld_IsNeitherTakenWithTheMembership_NorMissedByTheGuard()
    {
        // Left the kurin, the office stayed open, came back: deleting the old membership must not end it.
        await using (var context = new AppDbContext(_options))
        {
            context.Leaderships.Add(new Leadership
            {
                Type = LeadershipType.Kurin,
                KurinKey = _kurinKey,
                StartDate = new DateOnly(2020, 1, 1),
                LeadershipHistories =
                {
                    new LeadershipHistory { MemberKey = _person, Role = LeadershipRole.Hronikar, StartDate = new DateOnly(2024, 10, 1) }
                }
            });
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var archive = new KurinArchiveRepository(read);
        Assert.True(await archive.HoldsOpenOfficeSinceAsync(_person, _kurinKey, new DateOnly(2025, 6, 30)));
        Assert.False(await archive.HoldsOpenOfficeSinceAsync(_person, _kurinKey, new DateOnly(2024, 6, 30)));
        Assert.Empty(await archive.RemoveOfficesHeldAsync(_person, _kurinKey, new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30)));
    }

    [Fact]
    public async Task ThePersonsOfficesInADissolvedGurtok_KeepTheirGurtok()
    {
        await using (var context = new AppDbContext(_options))
        {
            context.Members.Add(new Member { MemberKey = _person, FirstName = "Устим", LastName = "Соловей", Email = "u@example.com", PhoneNumber = "+380501234567" });
            var group = await context.Groups.SingleAsync(g => g.GroupKey == _sokoly.GroupKey);
            group.DissolvedAtUtc = new DateTime(2025, 9, 1);
            context.Leaderships.Add(new Leadership
            {
                Type = LeadershipType.Group,
                GroupKey = group.GroupKey,
                StartDate = new DateOnly(2022, 9, 1),
                LeadershipHistories = { new LeadershipHistory { MemberKey = _person, Role = LeadershipRole.Hurtkoviy, StartDate = new DateOnly(2022, 9, 1), EndDate = new DateOnly(2025, 9, 1) } }
            });
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var member = await new MemberRepository(read).GetByKeyAsync(_person);

        var office = Assert.Single(member!.LeadershipHistories);
        Assert.Equal("Соколи", office.Leadership.Group?.Name);
        Assert.Equal(_kurinKey, office.Leadership.Group?.KurinKey);
    }
}
