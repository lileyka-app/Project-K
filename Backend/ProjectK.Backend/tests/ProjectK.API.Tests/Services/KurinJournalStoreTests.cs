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
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.AuditModule;
using Xunit;
using InfraUnitOfWork = ProjectK.Infrastructure.UnitOfWork.UnitOfWork;

namespace ProjectK.API.Tests.Services;

/// <summary>
/// The journal against a real relational database: an entry commits with the change it describes or
/// not at all, the filters and the own-group reach run in SQL, and a year-old entry is swept.
/// </summary>
public class KurinJournalStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Guid _kurinKey = Guid.NewGuid();

    public KurinJournalStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Close();

    private KurinAuditEvent Entry(
        DateTime occurredAtUtc,
        Guid? subjectMemberKey = null,
        Guid? subjectGroupKey = null,
        AuditArea area = AuditArea.Award,
        Guid? actorUserKey = null,
        Guid? kurinKey = null) => new()
        {
            KurinKey = kurinKey ?? _kurinKey,
            OccurredAtUtc = occurredAtUtc,
            ActorUserKey = actorUserKey,
            ActorName = "Марта",
            ActorRole = "KV:Vykhovnyk",
            Area = area,
            Action = AuditActions.Award.Added,
            SubjectMemberKey = subjectMemberKey,
            SubjectGroupKey = subjectGroupKey,
            SubjectKey = Guid.NewGuid(),
            SubjectLabel = "1-е відзначення",
            Changes = [new AuditChange("level", null, "First"), new AuditChange("phoneNumber", null, null, Redacted: true)]
        };

    private async Task SeedAsync(params KurinAuditEvent[] entries)
    {
        await using var context = new AppDbContext(_options);
        context.KurinAuditEvents.AddRange(entries);
        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<KurinAuditEvent>> ReadAsync(KurinJournalFilter filter, int skip = 0, int take = 50)
    {
        await using var context = new AppDbContext(_options);
        return await new KurinJournalReader(context).ReadAsync(filter, skip, take);
    }

    [Fact]
    public async Task AnEntry_CommitsWithTheHandlersOwnSave()
    {
        await using (var context = new AppDbContext(_options))
        {
            new KurinAuditWriter(context).Add(Entry(DateTime.UtcNow));
            context.WaitlistEntries.Add(Waitlisted(Guid.NewGuid()));

            await new InfraUnitOfWork(context).SaveChangesAsync();
        }

        await using var check = new AppDbContext(_options);
        var stored = Assert.Single(await check.KurinAuditEvents.ToListAsync());
        Assert.Equal(1, await check.WaitlistEntries.CountAsync());
        Assert.Equal(
            [new AuditChange("level", null, "First"), new AuditChange("phoneNumber", null, null, Redacted: true)],
            stored.Changes);
    }

    [Fact]
    public async Task AFailedSave_LeavesNoEntryBehind()
    {
        var takenKey = Guid.NewGuid();
        await using (var seed = new AppDbContext(_options))
        {
            seed.WaitlistEntries.Add(Waitlisted(takenKey));
            await seed.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            new KurinAuditWriter(context).Add(Entry(DateTime.UtcNow));
            context.WaitlistEntries.Add(Waitlisted(takenKey));

            await Assert.ThrowsAsync<DbUpdateException>(() => new InfraUnitOfWork(context).SaveChangesAsync());
        }

        await using var check = new AppDbContext(_options);
        Assert.Equal(0, await check.KurinAuditEvents.CountAsync());
    }

    [Fact]
    public async Task TheRead_IsTheKurinsOwn_NewestFirst_OnePageAtATime()
    {
        var now = DateTime.UtcNow;
        await SeedAsync(
            Entry(now.AddMinutes(-3)),
            Entry(now.AddMinutes(-1)),
            Entry(now.AddMinutes(-2)),
            Entry(now, kurinKey: Guid.NewGuid()));

        var firstPage = await ReadAsync(new KurinJournalFilter(_kurinKey), 0, 2);
        var secondPage = await ReadAsync(new KurinJournalFilter(_kurinKey), 2, 2);

        Assert.Equal(new[] { now.AddMinutes(-1), now.AddMinutes(-2) }, firstPage.Select(e => e.OccurredAtUtc));
        Assert.Equal(new[] { now.AddMinutes(-3) }, secondPage.Select(e => e.OccurredAtUtc));
    }

    [Fact]
    public async Task TheFilters_NarrowTheRead()
    {
        var now = DateTime.UtcNow;
        var memberKey = Guid.NewGuid();
        var actorKey = Guid.NewGuid();
        var match = Entry(now.AddDays(-2), subjectMemberKey: memberKey, area: AuditArea.Warning, actorUserKey: actorKey);
        await SeedAsync(
            match,
            Entry(now.AddDays(-2), subjectMemberKey: memberKey, area: AuditArea.Award, actorUserKey: actorKey),
            Entry(now.AddDays(-2), subjectMemberKey: Guid.NewGuid(), area: AuditArea.Warning, actorUserKey: actorKey),
            Entry(now.AddDays(-2), subjectMemberKey: memberKey, area: AuditArea.Warning, actorUserKey: Guid.NewGuid()),
            Entry(now.AddDays(-10), subjectMemberKey: memberKey, area: AuditArea.Warning, actorUserKey: actorKey),
            Entry(now, subjectMemberKey: memberKey, area: AuditArea.Warning, actorUserKey: actorKey));

        var read = await ReadAsync(new KurinJournalFilter(
            _kurinKey, AuditArea.Warning, memberKey, actorKey, now.AddDays(-3), now.AddDays(-1)));

        Assert.Equal(match.KurinAuditEventKey, Assert.Single(read).KurinAuditEventKey);
    }

    [Fact]
    public async Task AnOwnGroupReader_SeesHisGroupsAndHimself_NotTheKurinAsAWhole()
    {
        var now = DateTime.UtcNow;
        var ledGroup = Guid.NewGuid();
        var ownMember = Guid.NewGuid();
        var inHisGroup = Entry(now.AddMinutes(-1), subjectMemberKey: Guid.NewGuid(), subjectGroupKey: ledGroup);
        var aboutHim = Entry(now.AddMinutes(-2), subjectMemberKey: ownMember, subjectGroupKey: Guid.NewGuid());
        await SeedAsync(
            inHisGroup,
            aboutHim,
            Entry(now.AddMinutes(-3), subjectMemberKey: Guid.NewGuid(), subjectGroupKey: Guid.NewGuid()),
            Entry(now.AddMinutes(-4), area: AuditArea.Settings));

        var read = await ReadAsync(new KurinJournalFilter(_kurinKey, Visibility: new JournalVisibility([ledGroup], ownMember)));
        var withoutAPerson = await ReadAsync(new KurinJournalFilter(_kurinKey, Visibility: new JournalVisibility([ledGroup], null)));

        Assert.Equal(new[] { inHisGroup.KurinAuditEventKey, aboutHim.KurinAuditEventKey }, read.Select(e => e.KurinAuditEventKey));
        Assert.Equal(new[] { inHisGroup.KurinAuditEventKey }, withoutAPerson.Select(e => e.KurinAuditEventKey));
    }

    [Fact]
    public async Task TheSweep_DeletesEntriesOlderThanAYear_InBatches()
    {
        var now = DateTime.UtcNow;
        await SeedAsync(
            Entry(now.AddDays(-400)),
            Entry(now.AddDays(-380)),
            Entry(now.AddDays(-366)),
            Entry(now.AddDays(-364)),
            Entry(now));

        var sweep = typeof(AuditCleanupBackgroundService).GetMethod("DeleteExpiredJournalAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        int deleted;
        await using (var context = new AppDbContext(_options))
        {
            deleted = await (Task<int>)sweep.Invoke(null, [context, now.AddDays(-365), 2, CancellationToken.None])!;
        }

        Assert.Equal(3, deleted);
        await using var check = new AppDbContext(_options);
        Assert.Equal(2, await check.KurinAuditEvents.CountAsync());
        Assert.True(await check.KurinAuditEvents.AllAsync(e => e.OccurredAtUtc > now.AddDays(-365)));
    }

    [Fact]
    public async Task TheDailyCleanup_KeepsTheJournalForAYear()
    {
        var now = DateTime.UtcNow;
        await SeedAsync(Entry(now.AddDays(-366)), Entry(now.AddDays(-200)));

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        var service = new AuditCleanupBackgroundService(
            services.BuildServiceProvider(), new Mock<ILogger<AuditCleanupBackgroundService>>().Object);
        var cleanup = typeof(AuditCleanupBackgroundService).GetMethod("CleanupOldRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)cleanup.Invoke(service, [CancellationToken.None])!;

        await using var check = new AppDbContext(_options);
        var left = Assert.Single(await check.KurinAuditEvents.ToListAsync());
        Assert.True(left.OccurredAtUtc > now.AddDays(-365));
    }

    [Fact]
    public async Task TheReadBySubjects_TakesEveryKurin_OldestFirst_AndOnlyThoseRecords()
    {
        var now = DateTime.UtcNow;
        var first = Entry(now.AddMinutes(-1));
        var second = Entry(now.AddMinutes(-3), kurinKey: Guid.NewGuid());
        var sameRecord = Entry(now.AddMinutes(-2));
        sameRecord.SubjectKey = first.SubjectKey;
        await SeedAsync(first, second, sameRecord, Entry(now));

        await using var context = new AppDbContext(_options);
        var reader = new KurinJournalReader(context);
        var read = await reader.GetForSubjectsAsync([first.SubjectKey, second.SubjectKey]);

        Assert.Equal(
            new[] { second.KurinAuditEventKey, sameRecord.KurinAuditEventKey, first.KurinAuditEventKey },
            read.Select(e => e.KurinAuditEventKey));
        Assert.Empty(await reader.GetForSubjectsAsync([]));
    }

    private static WaitlistEntry Waitlisted(Guid key) => new()
    {
        WaitlistEntryKey = key,
        FirstName = "Олесь",
        LastName = "Юнак",
        Email = $"{key:N}@example.com",
        PhoneNumber = "+380501112233",
        DateOfBirth = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        VerificationStatus = WaitlistVerificationStatus.Submitted
    };
}
