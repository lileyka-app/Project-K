using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>
/// How many people a kurin holds is counted from its open memberships in the database — not from
/// whatever the context happens to track, and not from who has an account.
/// </summary>
public class KurinHeadcountTests
{
    private static AppDbContext NewContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
            .UseInMemoryDatabase(name)
            .Options);

    private static Membership Joining(Guid kurinKey, Guid? userKey = null, DateTime? leftAtUtc = null) => new()
    {
        MemberKey = Guid.NewGuid(),
        UserKey = userKey,
        KurinKey = kurinKey,
        Kind = MembershipKind.Youth,
        JoinedAtUtc = DateTime.UtcNow.AddYears(-1),
        LeftAtUtc = leftAtUtc
    };

    [Fact]
    public async Task AKurinWithThreeCurrentMembers_ShouldCountThree_InBothReads()
    {
        var name = Guid.NewGuid().ToString();
        var kurinKey = Guid.NewGuid();
        var otherKurinKey = Guid.NewGuid();
        var emptyKurinKey = Guid.NewGuid();

        await using (var seeding = NewContext(name))
        {
            seeding.Memberships.AddRange(
                Joining(kurinKey, userKey: Guid.NewGuid()),
                Joining(kurinKey),
                Joining(kurinKey),
                Joining(kurinKey, leftAtUtc: DateTime.UtcNow.AddDays(-3)),
                Joining(otherKurinKey));
            await seeding.SaveChangesAsync();
        }

        await using var context = NewContext(name);
        var uow = new InfraUnitOfWork(context);

        Assert.Equal(3, await uow.Memberships.CountCurrentInKurinAsync(kurinKey));

        var counts = await uow.Memberships.CountCurrentInKurinsAsync([kurinKey, otherKurinKey, emptyKurinKey]);
        Assert.Equal(3, counts[kurinKey]);
        Assert.Equal(1, counts[otherKurinKey]);
        Assert.False(counts.ContainsKey(emptyKurinKey));
    }

    [Fact]
    public async Task NoKurins_ShouldCountNothing()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        var uow = new InfraUnitOfWork(context);

        Assert.Empty(await uow.Memberships.CountCurrentInKurinsAsync([]));
    }
}
