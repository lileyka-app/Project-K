using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Migrations;
using ProjectK.Infrastructure.Repositories.AuditModule;
using Xunit;

namespace ProjectK.API.Tests.Services;

/// <summary>
/// The fold of the old probe and badge trails into the kurin's journal, run on a database seeded
/// with old rows. SQLite here: the same statements in its dialect, so the mapping is pinned in CI;
/// the SQL Server dialect was run by hand on a copy before release.
/// </summary>
public class ProgressTrailFoldTests : IDisposable
{
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly DateTime _start = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    public ProgressTrailFoldTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE "ProbeProgressAuditEvents" (
                "ProbeProgressAuditEventKey" TEXT NOT NULL PRIMARY KEY, "ProbeProgressKey" TEXT NOT NULL,
                "FromStatus" INTEGER NULL, "ToStatus" INTEGER NOT NULL, "Action" TEXT NOT NULL,
                "ActorUserKey" TEXT NULL, "ActorName" TEXT NULL, "ActorRole" TEXT NOT NULL,
                "OccurredAtUtc" TEXT NOT NULL, "Note" TEXT NULL);
            CREATE TABLE "BadgeProgressAuditEvents" (
                "BadgeProgressAuditEventKey" TEXT NOT NULL PRIMARY KEY, "BadgeProgressKey" TEXT NOT NULL,
                "FromStatus" INTEGER NULL, "ToStatus" INTEGER NOT NULL, "Action" TEXT NOT NULL,
                "ActorUserKey" TEXT NULL, "ActorName" TEXT NULL, "ActorRole" TEXT NOT NULL,
                "OccurredAtUtc" TEXT NOT NULL, "Note" TEXT NULL);
            """);
    }

    public void Dispose() => _connection.Close();

    private static string Key(Guid value) => value.ToString().ToUpperInvariant();

    private static string Time(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

    private async Task<Guid> OldRowAsync(
        string table, Guid progressKey, int? from, int to, string action, int minutes, string? note = null, Guid? actorUserKey = null)
    {
        var key = Guid.NewGuid();
        var keyColumn = table == "ProbeProgressAuditEvents" ? "ProbeProgressAuditEventKey" : "BadgeProgressAuditEventKey";
        var progressColumn = table == "ProbeProgressAuditEvents" ? "ProbeProgressKey" : "BadgeProgressKey";

        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO "{table}" ("{keyColumn}", "{progressColumn}", "FromStatus", "ToStatus", "Action",
                "ActorUserKey", "ActorName", "ActorRole", "OccurredAtUtc", "Note")
            VALUES ($key, $progress, $from, $to, $action, $actor, 'Марта', 'KV:Vykhovnyk', $at, $note)
            """;
        command.Parameters.AddWithValue("$key", Key(key));
        command.Parameters.AddWithValue("$progress", Key(progressKey));
        command.Parameters.AddWithValue("$from", (object?)from ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", to);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$actor", actorUserKey is { } actor ? Key(actor) : DBNull.Value);
        command.Parameters.AddWithValue("$at", Time(_start.AddMinutes(minutes)));
        command.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return key;
    }

    private async Task FoldAsync()
    {
        // Straight to the connection: the statements carry JSON, whose braces EF's raw SQL would read
        // as placeholders.
        foreach (var statement in FoldProgressTrailsIntoKurinJournal.FoldStatements(SqliteProvider))
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<(Member Member, ProbeProgress Probe, BadgeProgress Badge)> SeedPersonAsync()
    {
        var member = new Member
        {
            FirstName = "Олесь",
            LastName = "Юнак",
            Email = "o@example.com",
            PhoneNumber = "",
            DateOfBirth = new DateOnly(2012, 1, 1)
        };
        var probe = new ProbeProgress { MemberKey = member.MemberKey, KurinKey = _kurinKey, ProbeId = "probe-1", Status = ProbeProgressStatus.Completed };
        var badge = new BadgeProgress { MemberKey = member.MemberKey, KurinKey = _kurinKey, BadgeId = "badge-1", Status = BadgeProgressStatus.Rejected };

        await using var context = new AppDbContext(_options);
        context.Members.Add(member);
        context.Memberships.AddRange(
            new Membership { MemberKey = member.MemberKey, KurinKey = _kurinKey, GroupKey = _groupKey, JoinedAtUtc = _start },
            new Membership { MemberKey = member.MemberKey, KurinKey = _kurinKey, GroupKey = Guid.NewGuid(), JoinedAtUtc = _start.AddYears(-3), LeftAtUtc = _start.AddYears(-2) });
        context.ProbeProgresses.Add(probe);
        context.BadgeProgresses.Add(badge);
        await context.SaveChangesAsync();
        return (member, probe, badge);
    }

    [Fact]
    public async Task EveryOldRow_BecomesTheEntryTheHandlersWriteToday()
    {
        var (member, probe, _) = await SeedPersonAsync();
        var actorUserKey = Guid.NewGuid();
        var started = await OldRowAsync("ProbeProgressAuditEvents", probe.ProbeProgressKey, 0, 1, "InProgress", 0, actorUserKey: actorUserKey);
        await OldRowAsync("ProbeProgressAuditEvents", probe.ProbeProgressKey, 1, 2, "Completed", 1, "усі");

        await FoldAsync();

        await using var context = new AppDbContext(_options);
        var entries = await new KurinJournalReader(context).GetForSubjectsAsync([probe.ProbeProgressKey]);
        Assert.Equal([AuditActions.Probe.Started, AuditActions.Probe.Completed], entries.Select(e => e.Action));

        var first = entries[0];
        Assert.Equal(started, first.KurinAuditEventKey);
        Assert.Equal(_kurinKey, first.KurinKey);
        Assert.Equal(_start, first.OccurredAtUtc);
        Assert.Equal(actorUserKey, first.ActorUserKey);
        Assert.Equal("Марта", first.ActorName);
        Assert.Equal("KV:Vykhovnyk", first.ActorRole);
        Assert.Equal(AuditArea.Probe, first.Area);
        Assert.Equal(member.MemberKey, first.SubjectMemberKey);
        Assert.Equal("Олесь Юнак", first.SubjectMemberName);
        Assert.Equal(_groupKey, first.SubjectGroupKey);
        Assert.Equal("probe-1", first.SubjectLabel);
        Assert.Equal([new AuditChange("status", "NotStarted", "InProgress")], first.Changes);
        Assert.Equal("усі", entries[1].Note);
    }

    [Theory]
    [InlineData(0, 1, "InProgress", AuditActions.Probe.Started)]
    [InlineData(1, 2, "Completed", AuditActions.Probe.Completed)]
    [InlineData(2, 3, "Verified", AuditActions.Probe.Verified)]
    [InlineData(3, 2, "Unverify", AuditActions.Probe.Unverified)]
    [InlineData(2, 1, "Unsign", AuditActions.Probe.Reopened)]
    [InlineData(3, 1, "Unsign", AuditActions.Probe.Reopened)]
    public async Task AProbeRow_FoldsToTheActionItsStatusesMean_AndReadsBackAsItWas(int from, int to, string oldAction, string action)
    {
        var (_, probe, _) = await SeedPersonAsync();
        await OldRowAsync("ProbeProgressAuditEvents", probe.ProbeProgressKey, from, to, oldAction, 0);

        await FoldAsync();

        await using var context = new AppDbContext(_options);
        var entry = Assert.Single(await new KurinJournalReader(context).GetForSubjectsAsync([probe.ProbeProgressKey]));
        Assert.Equal(action, entry.Action);
        var item = Assert.Single(ProgressTrail.ForProbe(probe.ProbeProgressKey, [entry]));
        Assert.Equal((ProbeProgressStatus)from, item.FromStatus);
        Assert.Equal((ProbeProgressStatus)to, item.ToStatus);
        Assert.Equal(oldAction, item.Action);
    }

    [Theory]
    [InlineData(null, 1, "Submitted", AuditActions.Badge.Submitted)]
    [InlineData(3, 1, "Resubmitted", AuditActions.Badge.Resubmitted)]
    [InlineData(1, 2, "Confirmed", AuditActions.Badge.Confirmed)]
    [InlineData(1, 3, "Rejected", AuditActions.Badge.Rejected)]
    [InlineData(2, 3, "RemovedConfirmed", AuditActions.Badge.ConfirmationRemoved)]
    public async Task ABadgeRow_FoldsToTheActionItsStatusesMean_AndReadsBackAsItWas(int? from, int to, string oldAction, string action)
    {
        var (_, _, badge) = await SeedPersonAsync();
        await OldRowAsync("BadgeProgressAuditEvents", badge.BadgeProgressKey, from, to, oldAction, 0, "так");

        await FoldAsync();

        await using var context = new AppDbContext(_options);
        var entry = Assert.Single(await new KurinJournalReader(context).GetForSubjectsAsync([badge.BadgeProgressKey]));
        Assert.Equal(action, entry.Action);
        Assert.Equal(AuditArea.Badge, entry.Area);
        Assert.Equal("badge-1", entry.SubjectLabel);
        var item = Assert.Single(ProgressTrail.ForBadge(badge.BadgeProgressKey, [entry]));
        Assert.Equal((BadgeProgressStatus?)from, item.FromStatus);
        Assert.Equal((BadgeProgressStatus)to, item.ToStatus);
        Assert.Equal(oldAction, item.Action);
        Assert.Equal("так", item.Note);
    }

    [Fact]
    public async Task ARowTheJournalAlreadyHolds_IsNotCopiedTwice()
    {
        var (member, probe, _) = await SeedPersonAsync();
        await OldRowAsync("ProbeProgressAuditEvents", probe.ProbeProgressKey, 1, 2, "Completed", 0);
        await using (var context = new AppDbContext(_options))
        {
            context.KurinAuditEvents.Add(new()
            {
                KurinKey = _kurinKey,
                OccurredAtUtc = _start.AddSeconds(1),
                ActorRole = "KV:Vykhovnyk",
                Area = AuditArea.Probe,
                Action = AuditActions.Probe.Completed,
                SubjectMemberKey = member.MemberKey,
                SubjectKey = probe.ProbeProgressKey,
                SubjectLabel = "Проба",
                Changes = [new AuditChange("status", "InProgress", "Completed")]
            });
            await context.SaveChangesAsync();
        }

        await FoldAsync();
        await FoldAsync();

        await using var check = new AppDbContext(_options);
        var entry = Assert.Single(await check.KurinAuditEvents.ToListAsync());
        Assert.Equal("Проба", entry.SubjectLabel);
    }

    [Fact]
    public async Task APersonNoLongerOnRecord_IsFoldedWithoutANameOrGroup()
    {
        var gone = new BadgeProgress { MemberKey = Guid.NewGuid(), KurinKey = _kurinKey, BadgeId = "badge-2", Status = BadgeProgressStatus.Submitted };
        await using (var context = new AppDbContext(_options))
        {
            context.BadgeProgresses.Add(gone);
            await context.SaveChangesAsync();
        }
        await OldRowAsync("BadgeProgressAuditEvents", gone.BadgeProgressKey, null, 1, "Submitted", 0, new string('x', 2500));

        await FoldAsync();

        await using var check = new AppDbContext(_options);
        var entry = Assert.Single(await check.KurinAuditEvents.ToListAsync());
        Assert.Equal(gone.MemberKey, entry.SubjectMemberKey);
        Assert.Null(entry.SubjectMemberName);
        Assert.Null(entry.SubjectGroupKey);
        Assert.Equal(2000, entry.Note!.Length);
        Assert.Equal([new AuditChange("status", null, "Submitted")], entry.Changes);
    }
}
