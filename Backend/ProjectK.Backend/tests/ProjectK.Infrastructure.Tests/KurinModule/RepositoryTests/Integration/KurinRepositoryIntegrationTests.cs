using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.UnitOfWork;

namespace ProjectK.BusinessLogic.Tests.KurinModule.RepositoryTests.Integration;

public class KurinRepositoryIntegrationTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_And_GetByKeyAsync_ShouldWorkCorrectly()
    {
        // Arrange
        var kurin = new Kurin(10);
        var kurinKey = kurin.KurinKey;

        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);

            // Act
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();

            // Assert
            var fetched = await unitOfWork.Kurins.GetByKeyAsync(kurinKey);
            Assert.NotNull(fetched);
            Assert.Equal(10, fetched!.Number);
            Assert.Equal(kurinKey, fetched.KurinKey);
        }
    }

    [Fact]
    public async Task Update_ShouldModifyExistingEntity()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);

            var kurin = new Kurin(5);
            var kurinKey = kurin.KurinKey;
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();

            // Act
            kurin.SetNumber(20);
            unitOfWork.Kurins.Update(kurin);
            await unitOfWork.SaveChangesAsync();

            // Assert
            var updated = await unitOfWork.Kurins.GetByKeyAsync(kurinKey);
            Assert.NotNull(updated);
            Assert.Equal(20, updated!.Number);
        }
    }

    [Fact]
    public async Task Delete_ShouldRemoveEntity()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);

            var kurin = new Kurin(7);
            var kurinKey = kurin.KurinKey;
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();

            // Act
            unitOfWork.Kurins.Delete(kurin);
            await unitOfWork.SaveChangesAsync();

            // Assert
            var deleted = await unitOfWork.Kurins.GetByKeyAsync(kurinKey);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task GetByNumberAsync_ShouldReturnCorrectEntity()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);
            var kurin = new Kurin(15);
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();
            // Act
            var fetched = await unitOfWork.Kurins.GetByNumberAsync("ua", 15);
            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(15, fetched!.Number);
        }
    }

    [Fact]
    public async Task GetByNumberAsync_ShouldLookOnlyInTheNamedKrai()
    {
        using var context = CreateInMemoryDbContext();
        var unitOfWork = new UnitOfWork(context);
        var ukrainian = new Kurin(15);
        var czech = new Kurin(15) { KraiCode = "cz" };
        unitOfWork.Kurins.Create(ukrainian);
        unitOfWork.Kurins.Create(czech);
        await unitOfWork.SaveChangesAsync();

        var fetched = await unitOfWork.Kurins.GetByNumberAsync("cz", 15);

        Assert.Equal(czech.KurinKey, fetched!.KurinKey);
        Assert.False(await unitOfWork.Kurins.ExistsAsync("pl", 15));
    }

    [Fact]
    public async Task IsNumberTakenAsync_ShouldCountEveryBranchOfAKurin_AndOnlyThatBranch()
    {
        using var context = CreateInMemoryDbContext();
        var unitOfWork = new UnitOfWork(context);
        var order = Kurin.InBranches([new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)]);
        unitOfWork.Kurins.Create(order);
        unitOfWork.Kurins.Create(new Kurin(5));
        await unitOfWork.SaveChangesAsync();

        Assert.True(await unitOfWork.Kurins.IsNumberTakenAsync("ua", KurinBranch.UPS, 20));
        Assert.True(await unitOfWork.Kurins.IsNumberTakenAsync("ua", KurinBranch.USP, 5));
        Assert.False(await unitOfWork.Kurins.IsNumberTakenAsync("ua", KurinBranch.UPS, 5));
        Assert.False(await unitOfWork.Kurins.IsNumberTakenAsync("ua", KurinBranch.UPS, 20, order.KurinKey));

        context.ChangeTracker.Clear();
        var read = await unitOfWork.Kurins.GetByKeyAsync(order.KurinKey);
        Assert.Equal([new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)], read!.Numbering);
    }

    [Fact]
    public void TheDatabase_ShouldHoldEachNumberOncePerKraiAndBranch_AndOneOpenRequestPerKurin()
    {
        using var context = CreateInMemoryDbContext();

        var numbers = context.Model.FindEntityType(typeof(KurinBranchNumber))!.GetIndexes()
            .Single(index => index.Properties.Select(p => p.Name).SequenceEqual(["KraiCode", "Branch", "Number"]));
        var open = context.Model.FindEntityType(typeof(KurinBranchRequest))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_KurinBranchRequests_KurinKey_Pending");

        Assert.True(numbers.IsUnique);
        Assert.True(open.IsUnique);
        Assert.Equal("[Status] = 0", open.GetFilter());
    }

    [Fact]
    public async Task TheSeederReset_ShouldTakeOnlyTheYouthKurin1_NeverASeniorOne()
    {
        using var context = CreateInMemoryDbContext();
        var senior = new Kurin(1, KurinBranch.USP);
        context.Kurins.Add(senior);
        await context.SaveChangesAsync();

        Assert.Null(await ProjectK.Infrastructure.Seeding.DataSeeder.FindSeederKurinAsync(context));

        var youth = new Kurin(1);
        context.Kurins.Add(youth);
        await context.SaveChangesAsync();

        Assert.Equal(youth.KurinKey, (await ProjectK.Infrastructure.Seeding.DataSeeder.FindSeederKurinAsync(context))!.KurinKey);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnCorrectEntities()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);
            unitOfWork.Kurins.Create(new Kurin(1));
            unitOfWork.Kurins.Create(new Kurin(2));
            await unitOfWork.SaveChangesAsync();
            // Act
            var allKurins = await unitOfWork.Kurins.GetAllAsync();
            // Assert
            Assert.NotNull(allKurins);
            Assert.Equal(2, allKurins.Count());
        }
    }

    [Fact]
    public async Task ExistsAsync_ByKey_ShouldReturnTrueForExistingEntity()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);
            var kurin = new Kurin(3);
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();
            // Act
            var exists = await unitOfWork.Kurins.ExistsAsync(kurin.KurinKey);
            // Assert
            Assert.True(exists);
        }
    }

    [Fact]
    public async Task ExistsAsync_ByNumber_ShouldReturnTrueForExistingEntity()
    {
        // Arrange
        using (var context = CreateInMemoryDbContext())
        {
            var unitOfWork = new UnitOfWork(context);
            var kurin = new Kurin(4);
            unitOfWork.Kurins.Create(kurin);
            await unitOfWork.SaveChangesAsync();
            // Act
            var exists = await unitOfWork.Kurins.ExistsAsync("ua", 4);
            // Assert
            Assert.True(exists);
        }
    }

    /// <summary>
    /// The sequence <c>DeleteKurinCommandHandler</c> runs, against a real change tracker — and the claim
    /// this release exists to make: a kurin closing is something that happens to the kurin. The
    /// person who belonged to it is still there afterwards, belonging nowhere, with everything
    /// they earned still theirs.
    /// </summary>
    [Fact]
    public async Task DeletingAKurin_ShouldTakeItsOfficesAndMemberships_ButNotThePeople()
    {
        using var context = CreateInMemoryDbContext();
        var uow = new UnitOfWork(context);

        var kurin = new Kurin(77);
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
            Email = "kurin-delete@projectk.com",
            PhoneNumber = "0500000000"
        };
        context.Members.Add(seeded);
        context.Memberships.Add(Infrastructure.Tests.KurinModule.RepositoryTests.Integration.Placing.Of(seeded, kurin.KurinKey, group.GroupKey));
        context.Leaderships.AddRange(
            new Leadership { LeadershipKey = Guid.NewGuid(), KurinKey = kurin.KurinKey },
            new Leadership { LeadershipKey = Guid.NewGuid(), GroupKey = group.GroupKey });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var tracked = await uow.Kurins.GetByKeyAsync(kurin.KurinKey);
        var offices = await uow.Leaderships.DeleteForKurinAsync(kurin.KurinKey);
        await uow.Memberships.RemoveForKurinAsync(kurin.KurinKey);

        uow.Kurins.Delete(tracked!);
        await uow.SaveChangesAsync();

        Assert.Equal(2, offices.Count);
        Assert.Empty(context.Leaderships);
        Assert.Empty(context.Memberships);
        Assert.False(await uow.Kurins.ExistsAsync(kurin.KurinKey));

        var stillThere = Assert.Single(context.Members);
        Assert.Equal(seeded.MemberKey, stillThere.MemberKey);
    }
}
