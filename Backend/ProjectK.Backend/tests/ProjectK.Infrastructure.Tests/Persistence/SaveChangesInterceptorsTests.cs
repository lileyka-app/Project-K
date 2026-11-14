using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;

namespace ProjectK.Infrastructure.Tests.Persistence;

/// <summary>
/// The public code and the гурток stints are written by interceptors, so a context whose options
/// were built without them would save quietly wrong rows. These tests build the context the way the
/// host and the design-time tooling do.
/// </summary>
public class SaveChangesInterceptorsTests
{
    [Fact]
    public async Task HostContext_StampsPublicId_AndWritesStint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;Trusted_Connection=True"
            })
            .Build());
        // Runs after the host's interceptors and stops the save before it reaches SQL Server.
        services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new SuppressSave()));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var member = new Member { MemberKey = Guid.NewGuid(), FirstName = "Устим", LastName = "Соловей", Email = "u@example.com", PhoneNumber = "+380501234567" };
        var groupKey = Guid.NewGuid();
        var joined = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var membership = new Membership { MemberKey = member.MemberKey, KurinKey = Guid.NewGuid(), GroupKey = groupKey, JoinedAtUtc = joined };
        context.Members.Add(member);
        context.Memberships.Add(membership);

        await context.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(member.PublicId));
        var stint = Assert.Single(context.MembershipGroupStints.Local);
        Assert.Equal(membership.MembershipKey, stint.MembershipKey);
        Assert.Equal(groupKey, stint.GroupKey);
        Assert.Equal(joined, stint.FromUtc);
        Assert.Null(stint.ToUtc);
    }

    [Fact]
    public void DesignTimeContext_CarriesTheSameInterceptors()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);

        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!;

        Assert.Equal(
            [typeof(MemberPublicIdInterceptor), typeof(MembershipGroupStintInterceptor)],
            interceptors.Select(interceptor => interceptor.GetType()));
    }

    private sealed class SuppressSave : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => InterceptionResult<int>.SuppressWithResult(0);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
}
