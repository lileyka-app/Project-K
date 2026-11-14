using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Infrastructure.DbContexts;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

public class GroupRepositoryIntegrationTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_And_GetByKeyAsync_ShouldPersistAndReturnWithNavigation()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(11);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Alpha", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var fetched = await uow.Groups.GetByKeyAsync(group.GroupKey);

        Assert.NotNull(fetched);
        Assert.Equal(group.GroupKey, fetched!.GroupKey);
        Assert.Equal("Alpha", fetched.Name);
        Assert.Equal(kurin.KurinKey, fetched.KurinKey);
        Assert.NotNull(fetched.Kurin);
        Assert.Equal(kurin.Number, fetched.Kurin.Number);
    }

    [Fact]
    public async Task GetAllAsync_ByKurinKey_ShouldReturnOnlyThatKurinGroups()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin1 = new Kurin(1);
        var kurin2 = new Kurin(2);
        uow.Kurins.Create(kurin1);
        uow.Kurins.Create(kurin2);
        await uow.SaveChangesAsync();

        uow.Groups.Create(new Group("G1", kurin1.KurinKey));
        uow.Groups.Create(new Group("G2", kurin1.KurinKey));
        uow.Groups.Create(new Group("G3", kurin2.KurinKey));
        await uow.SaveChangesAsync();

        var kurin1Groups = (await uow.Groups.GetAllAsync(kurin1.KurinKey)).ToList();

        Assert.Equal(2, kurin1Groups.Count);
        Assert.All(kurin1Groups, g => Assert.Equal(kurin1.KurinKey, g.KurinKey));
        Assert.All(kurin1Groups, g => Assert.NotNull(g.Kurin));
        Assert.All(kurin1Groups, g => Assert.Equal(kurin1.Number, g.Kurin.Number));
    }

    [Fact]
    public async Task GetAllAsync_WhenNoneExist_ShouldReturnEmpty()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(5);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var groups = await uow.Groups.GetAllAsync(kurin.KurinKey);

        Assert.NotNull(groups);
        Assert.Empty(groups);
    }

    [Fact]
    public async Task Update_ShouldModifyGroupName()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(9);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Old", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        group.Name = "NewName";
        uow.Groups.Update(group);
        await uow.SaveChangesAsync();

        var fetched = await uow.Groups.GetByKeyAsync(group.GroupKey);
        Assert.NotNull(fetched);
        Assert.Equal("NewName", fetched!.Name);
    }

    [Fact]
    public async Task Delete_ShouldRemoveGroup()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(3);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("ToDelete", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        uow.Groups.Delete(group);
        await uow.SaveChangesAsync();

        var fetched = await uow.Groups.GetByKeyAsync(group.GroupKey);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingGroup()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(8);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Present", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var exists = await uow.Groups.ExistsAsync(group.GroupKey);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetAllAsync_Parameterless_ShouldThrowNotSupported()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await uow.Groups.GetAllAsync();
        });
    }

    /// <summary>
    /// The sequence <c>DeleteGroupCommandHandler</c> runs, against a real change tracker.
    /// <para>
    /// Members used to arrive <c>AsNoTracking</c> with their own detached <see cref="Group"/>
    /// attached; removing one then put a second instance of the already-tracked гурток in front
    /// of EF, which threw and the endpoint answered 500. Mocked handler tests cannot see this —
    /// only a real context can.
    /// </para>
    /// </summary>
    [Fact]
    public async Task DeletingAGroupWithMembers_ShouldNotConflictOverTheTrackedGroup()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(12);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Ведмеді", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var seeded = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "Тест",
            LastName = "Учасник",
            Email = "test@projectk.com",
            PhoneNumber = "0500000000"
        };
        context.Members.Add(seeded);
        context.Memberships.Add(Placing.Of(seeded, kurin.KurinKey, group.GroupKey));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var tracked = await uow.Groups.GetByKeyAsync(group.GroupKey);
        await uow.Memberships.DetachFromGroupAsync(group.GroupKey);

        uow.Groups.Delete(tracked!);
        await uow.SaveChangesAsync();

        Assert.False(await uow.Groups.ExistsAsync(group.GroupKey));

        // The гурток is gone; the person is not, and neither is their place in the kurin.
        var membership = Assert.Single(context.Memberships);
        Assert.Null(membership.GroupKey);
        Assert.Equal(kurin.KurinKey, membership.KurinKey);
        Assert.Single(context.Members);
    }

    /// <summary>
    /// Deleting a гурток must take its agenda assignments with it.
    /// <para>
    /// <c>AgendaAssignment.TargetKey</c> names a kurin, гурток, member or провід with a bare key
    /// and no foreign key, so nothing in the database clears it. A row left pointing at a deleted
    /// гурток reaches nobody, yet still counts as the item's assignment — an item assigned only
    /// there quietly stops appearing for everyone below whole-kurin scope.
    /// </para>
    /// </summary>
    [Fact]
    public async Task DeletingAGroup_ShouldTakeItsAgendaAssignmentsWithIt()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new InfraUnitOfWork(context);

        var kurin = new Kurin(13);
        uow.Kurins.Create(kurin);
        await uow.SaveChangesAsync();

        var group = new Group("Соколи", kurin.KurinKey);
        uow.Groups.Create(group);
        await uow.SaveChangesAsync();

        var memberKey = Guid.NewGuid();
        var seeded = new Member
        {
            MemberKey = memberKey,
            FirstName = "Тест",
            LastName = "Учасник",
            Email = "assignments@projectk.com",
            PhoneNumber = "0500000000"
        };
        context.Members.Add(seeded);
        context.Memberships.Add(Placing.Of(seeded, kurin.KurinKey, group.GroupKey));

        var item = new AgendaItem { KurinKey = kurin.KurinKey, Title = "Сходина" };
        context.AgendaItems.Add(item);
        context.AgendaAssignments.AddRange(
            new AgendaAssignment { AgendaItemKey = item.AgendaItemKey, TargetType = AgendaTargetType.Group, TargetKey = group.GroupKey },
            new AgendaAssignment { AgendaItemKey = item.AgendaItemKey, TargetType = AgendaTargetType.Member, TargetKey = memberKey },
            new AgendaAssignment { AgendaItemKey = item.AgendaItemKey, TargetType = AgendaTargetType.Kurin, TargetKey = kurin.KurinKey });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await uow.AgendaItems.RemoveAssignmentsForTargetsAsync([group.GroupKey]);
        await uow.Memberships.DetachFromGroupAsync(group.GroupKey);

        uow.Groups.Delete((await uow.Groups.GetByKeyAsync(group.GroupKey))!);
        await uow.SaveChangesAsync();

        // The гурток's own assignment goes; the person keeps theirs, because the person stays.
        var left = context.AgendaAssignments
            .Where(a => a.AgendaItemKey == item.AgendaItemKey)
            .OrderBy(a => a.TargetType)
            .ToList();
        Assert.Equal(2, left.Count);
        Assert.Contains(left, a => a.TargetType == AgendaTargetType.Member && a.TargetKey == memberKey);
        Assert.Contains(left, a => a.TargetType == AgendaTargetType.Kurin);
    }
}
