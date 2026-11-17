using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>
/// What «Змінити» on an automatic rule does to the rule's history. Found live: a провід set every
/// rule on the day they entered a year of history, could not push the start past that day, and
/// the history scored itself — the same rate from a later day used to add a row that changed
/// nothing, because the old row kept paying for the days in between.
/// </summary>
public sealed class ScoreRuleHandlerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private readonly List<ScoreRule> _rules = [];
    private readonly SetScoreRuleCommandHandler _handler;

    public ScoreRuleHandlerTests()
    {
        var rules = new Mock<IScoreRuleRepository>();
        rules.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [.. _rules]);
        rules.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _rules.First(x => x.ScoreRuleKey == key));
        rules.Setup(r => r.Create(It.IsAny<ScoreRule>(), It.IsAny<CancellationToken>()))
            .Callback((ScoreRule rule, CancellationToken _) => _rules.Add(rule));
        rules.Setup(r => r.Delete(It.IsAny<ScoreRule>(), It.IsAny<CancellationToken>()))
            .Callback((ScoreRule rule, CancellationToken _) => _rules.Remove(rule));

        var score = new Mock<IScoreUnitOfWork>();
        score.SetupGet(s => s.ScoreRules).Returns(rules.Object);
        score.SetupGet(s => s.ScoreTrailEvents).Returns(Mock.Of<IScoreTrailEventRepository>());
        score.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(Actor);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);

        // The caller manages the kurin's scoring; who may is not what these tests are about.
        var resourceAccess = new Mock<IResourceAccessService>();
        resourceAccess
            .Setup(r => r.CheckAccessAsync(ResourceType.KurinScore, ResourceAction.Manage, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        _handler = new SetScoreRuleCommandHandler(
            new ScoreAccess(user.Object, resourceAccess.Object),
            score.Object,
            user.Object,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    private ScoreRule Given(int points, DateOnly from)
    {
        var rule = new ScoreRule { KurinKey = Kurin, Source = ScoreSource.Skill, Variant = 0, Points = points, FromDate = from };
        _rules.Add(rule);
        return rule;
    }

    private Task<ServiceResult<MediatR.Unit>> Set(int points, DateOnly from) =>
        _handler.Handle(
            new SetScoreRuleCommand(Kurin, new SetScoreRuleRequest { Source = ScoreSource.Skill, Variant = 0, Points = points, FromDate = from }),
            CancellationToken.None);

    [Fact]
    public async Task TheSameRateFromAnotherDay_MovesTheRuleInForce()
    {
        var mistaken = Given(2, new DateOnly(2026, 10, 9));

        var result = await Set(2, new DateOnly(2026, 10, 11));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().ContainSingle();
        mistaken.FromDate.Should().Be(new DateOnly(2026, 10, 11), "nothing entered on the 9th and 10th counts any more");
    }

    [Fact]
    public async Task ADifferentRateFromALaterDay_StartsANewRule_AndKeepsTheOld()
    {
        var september = Given(2, new DateOnly(2026, 9, 1));

        var result = await Set(3, new DateOnly(2026, 10, 15));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().HaveCount(2);
        september.Points.Should().Be(2, "what was earned in September keeps its rate");
        _rules.Last().Should().Match<ScoreRule>(r => r.Points == 3 && r.FromDate == new DateOnly(2026, 10, 15));
    }

    [Fact]
    public async Task ADifferentRateFromAnEarlierDay_CorrectsTheRuleInForce()
    {
        var mistaken = Given(2, new DateOnly(2026, 10, 9));

        var result = await Set(3, new DateOnly(2026, 9, 1));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().ContainSingle();
        mistaken.Points.Should().Be(3);
        mistaken.FromDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public async Task TheSameDay_OnlyRetypesTheRule()
    {
        var rule = Given(2, new DateOnly(2026, 10, 9));

        var result = await Set(5, new DateOnly(2026, 10, 9));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().ContainSingle();
        rule.Points.Should().Be(5);
    }

    /// <summary>
    /// The case found on production: the old handler had added a second row with the same rate on
    /// a later day, and retyping the older one to that rate left the later one in force — so the
    /// page kept showing the later date whatever the провід entered.
    /// </summary>
    [Fact]
    public async Task RetypingAnOlderRuleToTheRateOfALaterDuplicate_DropsTheDuplicate()
    {
        var older = Given(0, new DateOnly(2026, 9, 1));
        Given(2, new DateOnly(2026, 10, 9));

        var result = await Set(2, new DateOnly(2026, 9, 1));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().ContainSingle().Which.Should().BeSameAs(older);
        older.Points.Should().Be(2);
        older.FromDate.Should().Be(new DateOnly(2026, 9, 1), "that is the day the rate started, and the day the page shows");
    }

    [Fact]
    public async Task ALaterRuleWithAnotherRate_IsKept()
    {
        Given(2, new DateOnly(2026, 9, 1));
        var later = Given(3, new DateOnly(2026, 10, 9));

        var result = await Set(5, new DateOnly(2026, 10, 9));

        result.Type.Should().Be(ResultType.Success);
        _rules.Should().HaveCount(2);
        later.Points.Should().Be(5);
    }

    [Fact]
    public async Task MovingTheRuleBackOverThePreviousOne_IsRefused()
    {
        Given(1, new DateOnly(2026, 9, 1));
        var latest = Given(2, new DateOnly(2026, 10, 9));

        var result = await Set(2, new DateOnly(2026, 8, 1));

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("ScoreRuleOverlap");
        latest.FromDate.Should().Be(new DateOnly(2026, 10, 9));
    }
}
