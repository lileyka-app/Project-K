using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;

namespace ProjectK.API.Tests.Services;

/// <summary>
/// A request between kurins goes a month after it was answered, withdrawn or lapsed; one still
/// waiting, or closed lately, stays.
/// </summary>
public class MembershipRequestCleanupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MembershipRequestCleanupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Close();

    private static MembershipRequest Request(DateTime createdAtUtc, DateTime expiresAtUtc) => new()
    {
        MemberKey = Guid.NewGuid(),
        FromKurinKey = Guid.NewGuid(),
        ToKurinKey = Guid.NewGuid(),
        CreatedAtUtc = createdAtUtc,
        ExpiresAtUtc = expiresAtUtc
    };

    [Fact]
    public async Task TheDailyCleanup_ForgetsRequests_AMonthAfterTheyClosed()
    {
        var now = DateTime.UtcNow;
        var approvedLongAgo = Request(now.AddDays(-50), now.AddDays(-36));
        approvedLongAgo.Close(MembershipRequestStatus.Approved, now.AddDays(-40), null);
        var declinedLately = Request(now.AddDays(-12), now.AddDays(2));
        declinedLately.Close(MembershipRequestStatus.Declined, now.AddDays(-10), null);
        var lapsedLongAgo = Request(now.AddDays(-60), now.AddDays(-46));
        var lapsedLately = Request(now.AddDays(-30), now.AddDays(-16));
        var waiting = Request(now.AddDays(-1), now.AddDays(13));

        await using (var seed = new AppDbContext(_options))
        {
            seed.MembershipRequests.AddRange(approvedLongAgo, declinedLately, lapsedLongAgo, lapsedLately, waiting);
            await seed.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection).AddAppDbContextInterceptors());
        var service = new AuditCleanupBackgroundService(
            services.BuildServiceProvider(), new Mock<ILogger<AuditCleanupBackgroundService>>().Object);
        var cleanup = typeof(AuditCleanupBackgroundService).GetMethod("CleanupOldRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)cleanup.Invoke(service, [CancellationToken.None])!;

        await using var check = new AppDbContext(_options);
        var left = await check.MembershipRequests.Select(r => r.MembershipRequestKey).ToListAsync();
        Assert.Equal(
            new[] { declinedLately.MembershipRequestKey, lapsedLately.MembershipRequestKey, waiting.MembershipRequestKey }.Order(),
            left.Order());
    }
}
