using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using Xunit;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>Two answers to one request read it while it was open; only the first is saved.</summary>
public class MembershipRequestConcurrencyTests
{
    [Fact]
    public async Task TheSecondOfTwoAnswersGivenAtOnce_IsRefused()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var request = new MembershipRequest
        {
            MemberKey = Guid.NewGuid(),
            FromKurinKey = Guid.NewGuid(),
            ToKurinKey = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        };
        await using (var seed = new AppDbContext(options))
        {
            seed.MembershipRequests.Add(request);
            await seed.SaveChangesAsync();
        }

        await using var approving = new AppDbContext(options);
        await using var declining = new AppDbContext(options);
        var approve = new InfraUnitOfWork(approving);
        var decline = new InfraUnitOfWork(declining);
        var approved = (await approve.MembershipRequests.GetAsync(request.MembershipRequestKey))!;
        var declined = (await decline.MembershipRequests.GetAsync(request.MembershipRequestKey))!;

        approved.Close(MembershipRequestStatus.Approved, DateTime.UtcNow, null);
        await approve.SaveChangesAsync();
        declined.Close(MembershipRequestStatus.Declined, DateTime.UtcNow, null);

        await Assert.ThrowsAsync<StaleRowException>(() => decline.SaveChangesAsync());
        await using var check = new AppDbContext(options);
        Assert.Equal(MembershipRequestStatus.Approved, (await check.MembershipRequests.SingleAsync()).Status);
    }
}
