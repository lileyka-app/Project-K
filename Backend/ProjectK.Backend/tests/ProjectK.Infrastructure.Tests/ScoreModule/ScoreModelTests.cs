using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Tests.ScoreModule;

/// <summary>
/// The "only once" rules of точкування are unique indexes, not checks in a handler, so two судді
/// marking at the same moment still cannot both win. The in-memory provider does not enforce them, so
/// this pins the model.
/// </summary>
public class ScoreModelTests
{
    private static IEntityType Entity<T>()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new AppDbContext(options);
        // The design-time model: the runtime one drops check constraints.
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;
    }

    private static IIndex Index<T>(string name) =>
        Entity<T>().GetIndexes().Single(i => i.GetDatabaseName() == name);

    [Fact]
    public void Attendance_IsOnePerPersonAndOccurrence_AmongTheMarksStillStanding()
    {
        var index = Index<ScoreAttendance>("IX_ScoreAttendances_OnePerOccurrence");

        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(ScoreAttendance.MembershipKey), nameof(ScoreAttendance.AgendaItemKey), nameof(ScoreAttendance.OccurrenceStartUtc)],
            index.Properties.Select(p => p.Name));
        Assert.Equal("[RemovedAtUtc] IS NULL", index.GetFilter());
    }

    [Theory]
    [InlineData("IX_ScoreEntries_ItemOncePerPerson", "MembershipKey")]
    [InlineData("IX_ScoreEntries_ItemOncePerGroup", "GroupKey")]
    public void APosition_IsGivenOnceAtAnEvent_WhileFreeEntriesAreNotLimited(string name, string target)
    {
        var index = Index<ScoreEntry>(name);

        Assert.True(index.IsUnique);
        Assert.Equal(
            [target, nameof(ScoreEntry.AgendaItemKey), nameof(ScoreEntry.OccurrenceStartUtc), nameof(ScoreEntry.ScoreItemKey)],
            index.Properties.Select(p => p.Name));
        Assert.Contains("[ScoreItemKey] IS NOT NULL", index.GetFilter());
        Assert.Contains("[DeletedAtUtc] IS NULL", index.GetFilter());
    }

    [Fact]
    public void AnEntry_IsForAPersonOrAGurtok_NeverBothOrNeither()
    {
        var check = Entity<ScoreEntry>().GetCheckConstraints().Single();

        Assert.Equal("CK_ScoreEntries_OneTarget", check.Name);
    }
}
