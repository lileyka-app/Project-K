using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.KurinModule;

namespace ProjectK.API.Tests.KurinModule;

/// <summary>
/// AGENDA-03: a youth of Соколи sees the сходини of Кельти only with «Графіки гуртків» on, and only
/// for a group marked «графік куреня». Run on SQLite so the category filter is translated for real.
/// </summary>
public class AgendaKurinScheduleVisibilityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Guid _kurin;
    private readonly Guid _sokoly = Guid.NewGuid();
    private readonly Guid _kelty = Guid.NewGuid();

    public AgendaKurinScheduleVisibilityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
        var kurin = new Kurin(1);
        _kurin = kurin.KurinKey;
        var schedule = new AgendaCategory { KurinKey = _kurin, Name = "Сходини", ColorHex = "#2F855A", IsKurinSchedule = true };
        var camps = new AgendaCategory { KurinKey = _kurin, Name = "Табори", ColorHex = "#B7791F" };
        context.Kurins.Add(kurin);
        context.AgendaCategories.AddRange(schedule, camps);
        context.AgendaItems.AddRange(
            Event("Сходини Соколів", _sokoly, schedule),
            Event("Сходини Кельтів", _kelty, schedule),
            Event("Табір Кельтів", _kelty, camps));
        context.SaveChanges();
    }

    public void Dispose() => _connection.Close();

    private AgendaItem Event(string title, Guid group, AgendaCategory category) => new()
    {
        KurinKey = _kurin,
        Kind = AgendaItemKind.Event,
        Title = title,
        StartUtc = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc),
        AgendaCategoryKey = category.AgendaCategoryKey,
        Assignments = [new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = group }]
    };

    private async Task<string[]> Seen(bool includeSchedules)
    {
        await using var context = new AppDbContext(_options);
        var youth = new AgendaViewerScope(_kurin, Guid.NewGuid(), Guid.NewGuid(), [_sokoly], [], false);
        var items = await new AgendaItemRepository(context).GetForViewerAsync(youth, null, null, true, null, default, includeKurinSchedules: includeSchedules);
        return items.Select(i => i.Title).OrderBy(t => t).ToArray();
    }

    [Fact]
    public async Task WithoutTheSchedules_AYouthSeesOnlyWhatIsAssigned()
    {
        (await Seen(false)).Should().Equal("Сходини Соколів");
    }

    [Fact]
    public async Task WithTheSchedules_EveryGurtoksSkhodynyShow_ButNotTheirOtherEvents()
    {
        (await Seen(true)).Should().Equal("Сходини Кельтів", "Сходини Соколів");
    }
}
