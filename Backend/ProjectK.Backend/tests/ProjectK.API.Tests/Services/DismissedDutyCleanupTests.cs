using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using Xunit;

namespace ProjectK.API.Tests.Services;

/// <summary>A duty someone hid comes back on its own a month later: the daily cleanup forgets the hiding.</summary>
public class DismissedDutyCleanupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DismissedDutyCleanupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Close();

    [Fact]
    public async Task TheDailyCleanup_ForgetsHiddenDuties_AfterAMonth()
    {
        var now = DateTime.UtcNow;
        var user = Guid.NewGuid();
        await using (var seed = new AppDbContext(_options))
        {
            seed.DismissedDuties.AddRange(
                new DismissedDuty { UserKey = user, DutyKey = "BadgesToReview:a:-:-:-", Fingerprint = "3", DismissedAtUtc = now.AddDays(-31) },
                new DismissedDuty { UserKey = user, DutyKey = "EntriesToVerify:a:-:-:-", Fingerprint = "1", DismissedAtUtc = now.AddDays(-29) });
            await seed.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        var service = new AuditCleanupBackgroundService(
            services.BuildServiceProvider(), new Mock<ILogger<AuditCleanupBackgroundService>>().Object);
        var cleanup = typeof(AuditCleanupBackgroundService).GetMethod("CleanupOldRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)cleanup.Invoke(service, [CancellationToken.None])!;

        await using var check = new AppDbContext(_options);
        var left = Assert.Single(await check.DismissedDuties.ToListAsync());
        Assert.Equal("EntriesToVerify:a:-:-:-", left.DutyKey);
    }
}
