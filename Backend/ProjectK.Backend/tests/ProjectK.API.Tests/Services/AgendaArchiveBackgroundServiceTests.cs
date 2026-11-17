using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;

namespace ProjectK.API.Tests.Services;

/// <summary>The nightly sweep follows each kurin's own periods, and never touches events.</summary>
public class AgendaArchiveBackgroundServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AgendaArchiveBackgroundServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors().UseSqlite(_connection).Options;
        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Close();

    private AgendaArchiveBackgroundService Service()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection).AddAppDbContextInterceptors());
        return new AgendaArchiveBackgroundService(services.BuildServiceProvider(), Mock.Of<ILogger<AgendaArchiveBackgroundService>>(), TimeProvider.System);
    }

    private static AgendaItem Task(Kurin kurin, string title, DateTime? completed = null, DateTime? archived = null, AgendaItemKind kind = AgendaItemKind.Task) => new()
    {
        KurinKey = kurin.KurinKey,
        Kind = kind,
        Title = title,
        Status = completed.HasValue ? AgendaItemStatus.Done : AgendaItemStatus.Todo,
        CompletedAtUtc = completed,
        ArchivedAtUtc = archived
    };

    [Fact]
    public async Task DoneTasksPastThePeriod_MoveToTheArchive_OldArchivedOnesAreDeleted()
    {
        using (var context = new AppDbContext(_options))
        {
            var kurin = new Kurin(1) { TaskAutoArchiveAfterDays = 30, TaskArchivePurgeAfterDays = 365 };
            var keeper = new Kurin(2) { TaskAutoArchiveAfterDays = null, TaskArchivePurgeAfterDays = null };
            context.Kurins.AddRange(kurin, keeper);
            context.AgendaItems.AddRange(
                Task(kurin, "Закрита давно", completed: Now.AddDays(-31)),
                Task(kurin, "Закрита нещодавно", completed: Now.AddDays(-10)),
                Task(kurin, "Ще в роботі"),
                Task(kurin, "Подія", completed: Now.AddDays(-90), kind: AgendaItemKind.Event),
                Task(kurin, "Давно в архіві", completed: Now.AddDays(-500), archived: Now.AddDays(-400)),
                Task(kurin, "Недавно в архіві", completed: Now.AddDays(-100), archived: Now.AddDays(-60)),
                Task(keeper, "Курінь без автоархіву", completed: Now.AddDays(-90)),
                Task(keeper, "Курінь без очищення", completed: Now.AddDays(-900), archived: Now.AddDays(-800)));
            await context.SaveChangesAsync();
        }

        await Service().SweepAsync(Now, CancellationToken.None);

        using var check = new AppDbContext(_options);
        var byTitle = await check.AgendaItems.ToDictionaryAsync(a => a.Title);
        byTitle["Закрита давно"].ArchivedAtUtc.Should().Be(Now);
        byTitle["Закрита нещодавно"].ArchivedAtUtc.Should().BeNull();
        byTitle["Ще в роботі"].ArchivedAtUtc.Should().BeNull();
        byTitle["Подія"].ArchivedAtUtc.Should().BeNull("events are the calendar's history");
        byTitle.Should().NotContainKey("Давно в архіві");
        byTitle["Недавно в архіві"].ArchivedAtUtc.Should().NotBeNull();
        byTitle["Курінь без автоархіву"].ArchivedAtUtc.Should().BeNull();
        byTitle.Should().ContainKey("Курінь без очищення");
    }
}
