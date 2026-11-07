using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.KurinModule;
using Xunit;

namespace ProjectK.Infrastructure.Tests.KurinModule;

/// <summary>
/// A person who left the kurin keeps their tag rows for a return, but carries nothing there now:
/// a «кожному окремо» task aimed at the tag must not wait on them, and the tag's count must not hold them.
/// </summary>
public class KurinTagCarriersFollowMembershipTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _stays = Guid.NewGuid();
    private readonly Guid _left = Guid.NewGuid();
    private readonly KurinTag _tag;
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    public KurinTagCarriersFollowMembershipTests()
    {
        _tag = new KurinTag { KurinKey = _kurinKey, Name = "Новики", ColorHex = "#2F855A" };
        using var context = new AppDbContext(_options);
        context.KurinTags.Add(_tag);
        context.MemberTags.AddRange(
            new MemberTag { KurinTagKey = _tag.KurinTagKey, MemberKey = _stays },
            new MemberTag { KurinTagKey = _tag.KurinTagKey, MemberKey = _left });
        context.Memberships.AddRange(
            new Membership { MemberKey = _stays, KurinKey = _kurinKey, JoinedAtUtc = DateTime.UtcNow.AddYears(-1) },
            new Membership { MemberKey = _left, KurinKey = _kurinKey, JoinedAtUtc = DateTime.UtcNow.AddYears(-1), LeftAtUtc = DateTime.UtcNow.AddDays(-1) });
        context.SaveChanges();
    }

    private KurinTagRepository Repository(AppDbContext context) => new(context);

    [Fact]
    public async Task TheRosterOfATag_HoldsOnlyWhoIsInTheKurinNow()
    {
        await using var context = new AppDbContext(_options);

        Assert.Equal([_stays], await Repository(context).GetMemberKeysAsync(_tag.KurinTagKey));
    }

    [Fact]
    public async Task TheKurinsAssignments_AndThePerPersonCounts_LeaveOutWhoLeft()
    {
        await using var context = new AppDbContext(_options);
        var repository = Repository(context);

        Assert.Equal([_stays], (await repository.GetAssignmentsAsync(_kurinKey)).Select(a => a.MemberKey));
        Assert.Equal([_stays], (await repository.GetAssignmentsAsync(_kurinKey, [_stays, _left])).Select(a => a.MemberKey));
        Assert.Equal([_stays], (await repository.CountPerMemberAsync(_kurinKey, [_stays, _left])).Keys);
    }

    [Fact]
    public async Task ThePersonWhoLeft_KeepsTheTagRowForAReturn()
    {
        await using var context = new AppDbContext(_options);

        Assert.True(await context.MemberTags.AnyAsync(a => a.MemberKey == _left));
    }
}
