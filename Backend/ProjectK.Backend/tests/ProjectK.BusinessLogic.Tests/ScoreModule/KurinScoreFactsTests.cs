using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Score;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>
/// What other modules know becomes facts about memberships: the вмілість is the person's, the points
/// go to the membership they held in this kurin on that day.
/// </summary>
public class KurinScoreFactsTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Oksana = Guid.NewGuid();
    private static readonly Guid FirstStay = Guid.NewGuid();
    private static readonly Guid SecondStay = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<KurinMembershipRecord> _memberships =
    [
        new(FirstStay, Oksana, Guid.NewGuid(), MembershipKind.Youth, new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
        new(SecondStay, Oksana, Guid.NewGuid(), MembershipKind.Youth, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), null)
    ];

    private KurinProgressFacts _progress = KurinProgressFacts.Empty;
    private readonly List<WarningRecord> _warnings = [];
    private readonly List<PaidQuarterRecord> _paid = [];

    private KurinScoreFacts Facts()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        var progress = new Mock<IMemberProgressDirectory>();
        progress.Setup(p => p.GetFactsForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _progress);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetActiveWarningsInKurinAsync(Kurin, Now, It.IsAny<CancellationToken>())).ReturnsAsync(() => _warnings);
        var dues = new Mock<IDuesDirectory>();
        dues.Setup(d => d.GetPaidQuartersAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _paid);
        return new KurinScoreFacts(memberships.Object, progress.Object, members.Object, dues.Object, new FixedTimeProvider(new DateTimeOffset(Now)));
    }

    [Fact]
    public async Task EachSource_BecomesAFact_WithItsDay()
    {
        _progress = new KurinProgressFacts(
            [new BadgeConfirmedRecord(Oksana, "cook", new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc))],
            [new ProbePointSignedRecord(Oksana, "p1", "pt3", new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc))],
            [new ProbeClosedRecord(Oksana, "p1", new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc))]);
        _warnings.Add(new WarningRecord(Oksana, MemberWarningLevel.Level2, new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc)));
        _paid.Add(new PaidQuarterRecord(SecondStay, 8107, new DateOnly(2026, 9, 30)));

        var facts = await Facts().ForKurinAsync(Kurin);

        facts.Should().BeEquivalentTo(new[]
        {
            new ScoreFact(SecondStay, ScoreSource.Skill, 0, new DateOnly(2026, 10, 1)),
            new ScoreFact(SecondStay, ScoreSource.ProbePoint, 0, new DateOnly(2026, 10, 2)),
            new ScoreFact(SecondStay, ScoreSource.Probe, 0, new DateOnly(2026, 10, 3)),
            new ScoreFact(SecondStay, ScoreSource.Warning, 2, new DateOnly(2026, 10, 4)),
            new ScoreFact(SecondStay, ScoreSource.Dues, 0, new DateOnly(2026, 9, 30))
        });
    }

    // Someone who left and came back: what they earned the first time stays with the first stay.
    [Fact]
    public async Task AFact_GoesToTheMembershipHeldThatDay_OrTheLatestOne()
    {
        _progress = new KurinProgressFacts(
            [
                new BadgeConfirmedRecord(Oksana, "old", new DateTime(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc)),
                new BadgeConfirmedRecord(Oksana, "between", new DateTime(2025, 12, 1, 9, 0, 0, DateTimeKind.Utc))
            ],
            [], []);

        var facts = await Facts().ForKurinAsync(Kurin);

        facts.Select(f => f.MembershipKey).Should().Equal(FirstStay, SecondStay);
    }

    [Fact]
    public async Task SomeoneWithNoYouthMembershipHere_EarnsNothing()
    {
        _progress = new KurinProgressFacts([new BadgeConfirmedRecord(Guid.NewGuid(), "x", Now)], [], []);

        (await Facts().ForKurinAsync(Kurin)).Should().BeEmpty();
    }
}
