using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.DuesModule;

namespace ProjectK.Infrastructure.Tests.DuesModule;

/// <summary>
/// The trail is written through the entry's collection on a tracked entry (see DuesEntryTrail). EF takes a
/// child that shows up with its key already set as an existing row and tries to UPDATE it, which fails
/// with a concurrency exception — so the key must be left to EF.
/// </summary>
public class DuesEntryTrailPersistenceTests
{
    [Fact]
    public async Task AnEventAddedToALoadedEntry_IsInserted()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var entryKey = Guid.NewGuid();

        await using (var context = new AppDbContext(options))
        {
            var entry = new DuesEntry
            {
                DuesEntryKey = entryKey,
                KurinKey = Guid.NewGuid(),
                GroupKey = Guid.NewGuid(),
                Kind = DuesEntryKind.Contribution,
                Amount = 300,
                OccurredOn = new DateOnly(2026, 10, 1)
            };
            entry.Events.Add(Event("Created"));
            context.DuesEntries.Add(entry);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var entry = await new DuesEntryRepository(context).GetByKeyAsync(entryKey);
            entry!.VerifiedAtUtc = DateTime.UtcNow;
            entry.Events.Add(Event("Verified"));
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var actions = await context.DuesEntryEvents
                .Where(e => e.DuesEntryKey == entryKey)
                .OrderBy(e => e.OccurredAtUtc)
                .Select(e => e.Action)
                .ToListAsync();

            Assert.Equal(["Created", "Verified"], actions);
        }
    }

    private static DuesEntryEvent Event(string action) =>
        new() { Action = action, ActorUserKey = Guid.NewGuid(), OccurredAtUtc = DateTime.UtcNow, Snapshot = "{}" };
}
