using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Roster;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories;
using ProjectK.Infrastructure.Repositories.AuthModule;
using ProjectK.Infrastructure.Repositories.InfrastructureModule;
using ProjectK.Infrastructure.Repositories.KurinModule;
using ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;
using Xunit;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

public class MemberRepositoryIntegrationTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Member Placed(InfraUnitOfWork uow, Member member, Guid kurinKey, Guid? groupKey = null)
    {
        uow.Members.Create(member);
        uow.Memberships.Create(Placing.Of(member, kurinKey, groupKey));
        return member;
    }

    private static Member BuildMember(Group _, Kurin __, string firstName = "Ivan", string lastName = "Petrenko", string middle = "I.")
        => new Member
        {
            FirstName = firstName,
            LastName = lastName,
            MiddleName = middle,
            Email = $"{firstName.ToLower()}@example.com",
            PhoneNumber = "123456",
            DateOfBirth = new DateOnly(2000, 1, 1)
        };

    [Fact]
    public async Task Create_And_GetByKeyAsync_ShouldPersistAndIncludeNavigation()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(12);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Alpha", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var member = BuildMember(group, kurin, "Oleh", "Shevchenko");
        Placed(uow, member, kurin.KurinKey, group.GroupKey);
        await uow.SaveChangesAsync();

        var fetched = await uow.Members.GetByKeyAsync(member.MemberKey);

        Assert.NotNull(fetched);
        Assert.Equal(member.MemberKey, fetched!.MemberKey);
    }

    [Fact]
    public async Task GetAllAsync_ByGroupKey_ShouldReturnOnlyThatGroupMembers()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(1);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group1 = new Group("G1", kurin.KurinKey);
        var group2 = new Group("G2", kurin.KurinKey);
        uow.Groups.Create(group1);
        uow.Groups.Create(group2);
        await uow.SaveChangesAsync();

        Placed(uow, BuildMember(group1, kurin, "A1", "L1"), kurin.KurinKey, group1.GroupKey);
        Placed(uow, BuildMember(group1, kurin, "A2", "L2"), kurin.KurinKey, group1.GroupKey);
        Placed(uow, BuildMember(group2, kurin, "B1", "L3"), kurin.KurinKey, group2.GroupKey);
        await uow.SaveChangesAsync();

        var group1Members = (await uow.Members.GetAllAsync(group1.GroupKey)).ToList();

        Assert.Equal(2, group1Members.Count);
        Assert.All(group1Members, m => Assert.Contains(context.Memberships, ms => ms.MemberKey == m.MemberKey && ms.GroupKey == group1.GroupKey));
    }

    [Fact]
    public async Task GetAllByKurinKeyAsync_ShouldReturnMembersAcrossGroups()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin1 = new Kurin(10);
        var kurin2 = new Kurin(20);
        uow.Kurins.Create(kurin1);
        uow.Kurins.Create(kurin2);
        await uow.SaveChangesAsync();

        var g1a = new Group("K1-G1", kurin1.KurinKey);
        var g1b = new Group("K1-G2", kurin1.KurinKey);
        var g2a = new Group("K2-G1", kurin2.KurinKey);
        uow.Groups.Create(g1a);
        uow.Groups.Create(g1b);
        uow.Groups.Create(g2a);
        await uow.SaveChangesAsync();

        Placed(uow, BuildMember(g1a, kurin1, "M1", "L1"), kurin1.KurinKey, g1a.GroupKey);
        Placed(uow, BuildMember(g1b, kurin1, "M2", "L2"), kurin1.KurinKey, g1b.GroupKey);
        Placed(uow, BuildMember(g2a, kurin2, "M3", "L3"), kurin2.KurinKey, g2a.GroupKey);
        await uow.SaveChangesAsync();

        var kurin1Members = (await uow.Members.GetAllByKurinKeyAsync(kurin1.KurinKey)).ToList();

        Assert.Equal(2, kurin1Members.Count);
        Assert.All(kurin1Members, m => Assert.Contains(context.Memberships, ms => ms.MemberKey == m.MemberKey && ms.KurinKey == kurin1.KurinKey));
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenMemberExists()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(5);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("GG", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var member = BuildMember(group, kurin, "Exist", "Test");
        Placed(uow, member, kurin.KurinKey, group.GroupKey);
        await uow.SaveChangesAsync();

        var exists = await uow.Members.ExistsAsync(member.MemberKey);
        var notExists = await uow.Members.ExistsAsync(Guid.NewGuid());

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task Update_ShouldModifyFields()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(2);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Alpha", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var member = BuildMember(group, kurin, "Old", "Name");
        Placed(uow, member, kurin.KurinKey, group.GroupKey);
        await uow.SaveChangesAsync();

        member.FirstName = "New";
        member.PhoneNumber = "999999";
        uow.Members.Update(member);
        await uow.SaveChangesAsync();

        var fetched = await uow.Members.GetByKeyAsync(member.MemberKey);
        Assert.NotNull(fetched);
        Assert.Equal("New", fetched!.FirstName);
        Assert.Equal("999999", fetched.PhoneNumber);
    }

    [Fact]
    public async Task Delete_ShouldRemoveMember()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(3);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Beta", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var member = BuildMember(group, kurin, "Del", "User");
        Placed(uow, member, kurin.KurinKey, group.GroupKey);
        await uow.SaveChangesAsync();

        uow.Members.Delete(member);
        await uow.SaveChangesAsync();

        var deleted = await uow.Members.GetByKeyAsync(member.MemberKey);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetListItemsByKurinKeyAsync_ShouldReturnOnlyActiveLeadershipAndWarnings()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(7);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Alpha", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var member = BuildMember(group, kurin, "Active", "Roles");
        Placed(uow, member, kurin.KurinKey, group.GroupKey);
        await uow.SaveChangesAsync();

        var leadership = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = LeadershipType.Group,
            Group = group,
            Name = "Alpha leadership",
            StartDate = new DateOnly(2024, 1, 1)
        };
        context.Set<Leadership>().Add(leadership);
        context.Set<LeadershipHistory>().AddRange(
            new LeadershipHistory
            {
                LeadershipHistoryKey = Guid.NewGuid(),
                MemberKey = member.MemberKey,
                LeadershipKey = leadership.LeadershipKey,
                Role = LeadershipRole.Hurtkoviy,
                StartDate = new DateOnly(2024, 1, 1),
                EndDate = null // active
            },
            new LeadershipHistory
            {
                LeadershipHistoryKey = Guid.NewGuid(),
                MemberKey = member.MemberKey,
                LeadershipKey = leadership.LeadershipKey,
                Role = LeadershipRole.Pysar,
                StartDate = new DateOnly(2022, 1, 1),
                EndDate = new DateOnly(2023, 1, 1) // archived
            });
        context.Set<MemberWarning>().AddRange(
            new MemberWarning
            {
                MemberKey = member.MemberKey,
                Level = MemberWarningLevel.Level2,
                IssuedAtUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                IssuedByUserKey = Guid.NewGuid(),
                RevokedAtUtc = null // active
            },
            new MemberWarning
            {
                MemberKey = member.MemberKey,
                Level = MemberWarningLevel.Level1,
                IssuedAtUtc = DateTime.UtcNow.AddDays(-10),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
                IssuedByUserKey = Guid.NewGuid(),
                RevokedAtUtc = DateTime.UtcNow.AddDays(-2) // revoked
            });
        await context.SaveChangesAsync();

        var items = (await uow.Members.GetListItemsByKurinKeyAsync(kurin.KurinKey)).ToList();

        var item = Assert.Single(items);
        var activeLeadership = Assert.Single(item.LeadershipHistories);
        Assert.Equal(LeadershipRole.Hurtkoviy, activeLeadership.Role);
        Assert.Equal(LeadershipType.Group, activeLeadership.LeadershipType);
        Assert.Equal(group.Name, activeLeadership.GroupName);

        var activeWarning = Assert.Single(item.Warnings);
        Assert.Equal(MemberWarningLevel.Level2, activeWarning.Level);
    }

    [Fact]
    public async Task GetListItemsByKurinKeyAsync_ShouldReadPrivateFieldsAsStored_LeavingTheMaskToTheQuery()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(8);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var ownGroup = new Group("Own", kurin.KurinKey);
        var visibleGroup = new Group("Visible", kurin.KurinKey);
        var hiddenGroup = new Group("Hidden", kurin.KurinKey);
        uow.Groups.Create(ownGroup);
        uow.Groups.Create(visibleGroup);
        uow.Groups.Create(hiddenGroup);
        await uow.SaveChangesAsync();

        var ownerUserKey = Guid.NewGuid();
        var owner = BuildMember(ownGroup, kurin, "Owner", "Self");
        owner.UserKey = ownerUserKey;
        owner.Address = "Owner St";
        owner.School = "Owner School";
        var inVisibleGroup = BuildMember(visibleGroup, kurin, "In", "VisibleGroup");
        inVisibleGroup.Address = "Visible St";
        inVisibleGroup.School = "Visible School";
        var hidden = BuildMember(hiddenGroup, kurin, "In", "HiddenGroup");
        hidden.Address = "Hidden St";
        hidden.School = "Hidden School";
        Placed(uow, owner, kurin.KurinKey, ownGroup.GroupKey);
        Placed(uow, inVisibleGroup, kurin.KurinKey, visibleGroup.GroupKey);
        Placed(uow, hidden, kurin.KurinKey, hiddenGroup.GroupKey);
        await uow.SaveChangesAsync();

        var items = (await uow.Members.GetListItemsByKurinKeyAsync(kurin.KurinKey)).ToList();

        var ownerItem = items.Single(i => i.MemberKey == owner.MemberKey);
        var visibleItem = items.Single(i => i.MemberKey == inVisibleGroup.MemberKey);
        var hiddenItem = items.Single(i => i.MemberKey == hidden.MemberKey);

        Assert.Equal("Owner St", ownerItem.Address);
        Assert.Equal("Visible St", visibleItem.Address);
        Assert.Equal("Hidden St", hiddenItem.Address);
        Assert.Equal("Hidden School", hiddenItem.School);
    }

    /// <summary>
    /// The split the реєстр draws. Only an office in the кадра виховників makes someone кадра: a
    /// гуртковий holds an office too and is still a юнак, and getting that wrong would empty the
    /// юнаки table of exactly the people who run the гуртки.
    /// </summary>
    [Fact]
    public async Task GetListItemsByKurinKeyAsync_ShouldCallOnlyKvOfficersStaff_AndNameTheGroupsTheyMentor()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(21);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var alpha = new Group("Alpha", kurin.KurinKey);
        var beta = new Group("Beta", kurin.KurinKey);
        uow.Groups.Create(alpha);
        uow.Groups.Create(beta);
        await uow.SaveChangesAsync();

        var youth = BuildMember(alpha, kurin, "Yura", "Yunak");
        var hurtkovyi = BuildMember(alpha, kurin, "Hanna", "Hurtkova");
        var mentor = BuildMember(alpha, kurin, "Marta", "Vykhovna");
        mentor.UserKey = Guid.NewGuid();
        Placed(uow, youth, kurin.KurinKey, alpha.GroupKey);
        Placed(uow, hurtkovyi, kurin.KurinKey, alpha.GroupKey);
        Placed(uow, mentor, kurin.KurinKey);
        await uow.SaveChangesAsync();

        var groupOffice = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = LeadershipType.Group,
            Group = alpha,
            Name = "Alpha",
            StartDate = new DateOnly(2024, 1, 1)
        };
        var kvOffice = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = LeadershipType.KV,
            KurinKey = kurin.KurinKey,
            Name = "KV",
            StartDate = new DateOnly(2024, 1, 1)
        };
        context.Set<Leadership>().AddRange(groupOffice, kvOffice);
        context.Set<LeadershipHistory>().AddRange(
            new LeadershipHistory
            {
                LeadershipHistoryKey = Guid.NewGuid(),
                MemberKey = hurtkovyi.MemberKey,
                LeadershipKey = groupOffice.LeadershipKey,
                Role = LeadershipRole.Hurtkoviy,
                StartDate = new DateOnly(2024, 1, 1)
            },
            new LeadershipHistory
            {
                LeadershipHistoryKey = Guid.NewGuid(),
                MemberKey = mentor.MemberKey,
                LeadershipKey = kvOffice.LeadershipKey,
                Role = LeadershipRole.Vykhovnyk,
                StartDate = new DateOnly(2024, 1, 1)
            });
        context.Set<MentorAssignment>().AddRange(
            new MentorAssignment
            {
                MentorUserKey = mentor.UserKey!.Value,
                GroupKey = beta.GroupKey,
                AssignedAtUtc = DateTime.UtcNow.AddDays(-10)
            },
            new MentorAssignment
            {
                MentorUserKey = mentor.UserKey!.Value,
                GroupKey = alpha.GroupKey,
                AssignedAtUtc = DateTime.UtcNow.AddDays(-20),
                RevokedAtUtc = DateTime.UtcNow.AddDays(-1) // handed the гурток over
            });
        await context.SaveChangesAsync();

        var items = (await uow.Members.GetListItemsByKurinKeyAsync(kurin.KurinKey)).ToList();

        var plainItem = items.Single(item => item.MemberKey == youth.MemberKey);
        var hurtkovyiItem = items.Single(item => item.MemberKey == hurtkovyi.MemberKey);
        var mentorItem = items.Single(item => item.MemberKey == mentor.MemberKey);

        Assert.False(plainItem.IsStaff);
        Assert.False(hurtkovyiItem.IsStaff);
        Assert.True(mentorItem.IsStaff);

        Assert.Empty(hurtkovyiItem.MentoredGroupNames);
        Assert.Equal(["Beta"], mentorItem.MentoredGroupNames);
    }

    /// <summary>
    /// The реєстр decides кадра in SQL and the звіт куреня decides it in memory, so the rule
    /// itself lives in <see cref="KurinRoster"/> and neither owns it. This is what stops the two
    /// from drifting: the projection's answer is compared against the rule applied to the very
    /// same offices, over a fixture that has one of each kind — including the two that look like
    /// кадра and are not.
    /// </summary>
    [Fact]
    public async Task GetListItemsByKurinKeyAsync_ShouldAgreeWithTheSharedStaffRule()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(31);
        var elsewhere = new Kurin(32);
        uow.Kurins.Create(kurin);
        uow.Kurins.Create(elsewhere);
        await uow.SaveChangesAsync();

        var group = new Group("Alpha", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var plain = BuildMember(group, kurin, "Yura", "Plain");
        var hurtkovyi = BuildMember(group, kurin, "Hanna", "Hurtkova");
        var kurinnyi = BuildMember(group, kurin, "Ostap", "Kurinnyi");
        var mentor = BuildMember(group, kurin, "Marta", "Mentor");
        var former = BuildMember(group, kurin, "Olena", "Former");
        var elsewhereMentor = BuildMember(group, kurin, "Ivan", "Foreign");
        foreach (var person in new[] { plain, hurtkovyi, kurinnyi, mentor, former, elsewhereMentor })
        {
            Placed(uow, person, kurin.KurinKey, group.GroupKey);
        }

        await uow.SaveChangesAsync();

        Leadership Office(LeadershipType type, Guid? kurinKey, Group? scope, DateOnly? closed = null) => new()
        {
            LeadershipKey = Guid.NewGuid(),
            Type = type,
            KurinKey = kurinKey,
            Group = scope,
            Name = type.ToString(),
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = closed
        };

        var groupOffice = Office(LeadershipType.Group, null, group);
        var kurinOffice = Office(LeadershipType.Kurin, kurin.KurinKey, null);
        var kvOffice = Office(LeadershipType.KV, kurin.KurinKey, null);
        var foreignKvOffice = Office(LeadershipType.KV, elsewhere.KurinKey, null);
        context.Set<Leadership>().AddRange(groupOffice, kurinOffice, kvOffice, foreignKvOffice);

        LeadershipHistory Held(Member person, Leadership office, LeadershipRole role, DateOnly? until = null) => new()
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            MemberKey = person.MemberKey,
            LeadershipKey = office.LeadershipKey,
            Role = role,
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = until
        };

        context.Set<LeadershipHistory>().AddRange(
            Held(hurtkovyi, groupOffice, LeadershipRole.Hurtkoviy),
            Held(kurinnyi, kurinOffice, LeadershipRole.Kurinnuy),
            Held(mentor, kvOffice, LeadershipRole.Vykhovnyk),
            Held(former, kvOffice, LeadershipRole.Vykhovnyk, until: new DateOnly(2025, 6, 1)),
            Held(elsewhereMentor, foreignKvOffice, LeadershipRole.Vykhovnyk));
        await context.SaveChangesAsync();

        var items = (await uow.Members.GetListItemsByKurinKeyAsync(kurin.KurinKey)).ToList();

        var offices = context.Set<Leadership>().ToDictionary(office => office.LeadershipKey);
        var expected = context.Set<LeadershipHistory>()
            .AsEnumerable()
            .Where(history => KurinRoster.IsStaffOffice(
                offices[history.LeadershipKey].Type,
                offices[history.LeadershipKey].KurinKey,
                offices[history.LeadershipKey].EndDate,
                history.EndDate,
                kurin.KurinKey))
            .Select(history => history.MemberKey)
            .ToHashSet();

        var actual = items.Where(item => item.IsStaff).Select(item => item.MemberKey).ToHashSet();

        Assert.Equal(expected, actual);
        Assert.Equal([mentor.MemberKey], actual);
    }

    [Fact]
    public async Task GetAllAsync_Parameterless_ShouldThrowNotSupported()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new MemberRepository(context);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            _ = await repo.GetAllAsync(); // parameterless
        });
    }
}
