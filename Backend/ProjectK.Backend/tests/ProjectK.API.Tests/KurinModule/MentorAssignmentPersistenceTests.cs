using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.KurinModule;

namespace ProjectK.API.Tests.KurinModule;

/// <summary>
/// Runs against a real relational schema, because the rule under test lives in a unique index
/// that the in-memory provider does not enforce.
/// </summary>
public class MentorAssignmentPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _mentorUserKey = Guid.NewGuid();

    public MentorAssignmentPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
        context.Groups.Add(new Group("G", Guid.NewGuid()) { GroupKey = _groupKey });
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Close();
    }

    private MentorAssignment Assignment(DateTime assignedAtUtc, DateTime? revokedAtUtc = null) => new()
    {
        MentorAssignmentKey = Guid.NewGuid(),
        MentorUserKey = _mentorUserKey,
        GroupKey = _groupKey,
        AssignedAtUtc = assignedAtUtc,
        RevokedAtUtc = revokedAtUtc
    };

    [Fact]
    public async Task ReassigningAfterRevoke_IsSavedAndKeepsTheEarlierPeriod()
    {
        await using (var context = new AppDbContext(_options))
        {
            context.MentorAssignments.Add(Assignment(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(-1)));
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(_options))
        {
            context.MentorAssignments.Add(Assignment(DateTime.UtcNow));
            var save = () => context.SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }

        await using var check = new AppDbContext(_options);
        var rows = await check.MentorAssignments.Where(a => a.GroupKey == _groupKey).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Count(a => a.RevokedAtUtc == null).Should().Be(1);
    }

    [Fact]
    public async Task TwoActiveAssignmentsOfTheSamePair_AreRejected()
    {
        await using var context = new AppDbContext(_options);
        context.MentorAssignments.Add(Assignment(DateTime.UtcNow.AddDays(-1)));
        context.MentorAssignments.Add(Assignment(DateTime.UtcNow));

        var save = () => context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetSpecificAssignment_PrefersTheActiveRow()
    {
        var active = Assignment(DateTime.UtcNow);
        await using (var context = new AppDbContext(_options))
        {
            context.MentorAssignments.Add(Assignment(DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-4)));
            context.MentorAssignments.Add(active);
            context.MentorAssignments.Add(Assignment(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(-1)));
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var found = await new MentorAssignmentRepository(read).GetSpecificAssignmentAsync(_mentorUserKey, _groupKey);

        found!.MentorAssignmentKey.Should().Be(active.MentorAssignmentKey);
    }

    [Fact]
    public async Task GetSpecificAssignment_WithOnlyRevokedRows_ReturnsTheLatestRevoked()
    {
        var latest = Assignment(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(-1));
        await using (var context = new AppDbContext(_options))
        {
            context.MentorAssignments.Add(Assignment(DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-4)));
            context.MentorAssignments.Add(latest);
            await context.SaveChangesAsync();
        }

        await using var read = new AppDbContext(_options);
        var found = await new MentorAssignmentRepository(read).GetSpecificAssignmentAsync(_mentorUserKey, _groupKey);

        found!.MentorAssignmentKey.Should().Be(latest.MentorAssignmentKey);
    }
}
