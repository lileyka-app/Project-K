using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ScoreModule.Events;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>A membership forgets its old гурток; the score has to remember it, or the points follow the person.</summary>
public class ScoreGroupMoveTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Levy = Guid.NewGuid();

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly List<ScoreGroupMove> _created = [];
    private readonly Mock<IScoreUnitOfWork> _score = new();

    public ScoreGroupMoveTests()
    {
        var moves = new Mock<IScoreGroupMoveRepository>();
        moves.Setup(r => r.Create(It.IsAny<ScoreGroupMove>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreGroupMove, CancellationToken>((move, _) => _created.Add(move));
        _score.SetupGet(s => s.ScoreGroupMoves).Returns(moves.Object);
    }

    private Task Moved(Guid? from, Guid? to, Guid membership) =>
        new RecordScoreGroupMoveEventHandler(_score.Object, _time).Handle(
            new DomainEventNotification<MembershipMovedToGroup>(new MembershipMovedToGroup(membership, Kurin, from, to)),
            CancellationToken.None);

    [Fact]
    public async Task AMove_IsWrittenDown_WithWhereFromAndWhen()
    {
        var membership = Guid.NewGuid();

        await Moved(Sokoly, Levy, membership);

        _created.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            KurinKey = Kurin,
            MembershipKey = membership,
            FromGroupKey = (Guid?)Sokoly,
            ToGroupKey = (Guid?)Levy,
            MovedAtUtc = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc)
        });
        _score.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never(),
            "the move commits with the command that made it");
    }

    [Fact]
    public async Task IntoAndOutOfAGurtok_IsAMoveToo()
    {
        await Moved(null, Sokoly, Guid.NewGuid());
        await Moved(Sokoly, null, Guid.NewGuid());

        _created.Should().HaveCount(2);
    }

    [Fact]
    public async Task StayingPut_IsNotAMove()
    {
        await Moved(Sokoly, Sokoly, Guid.NewGuid());

        _created.Should().BeEmpty();
    }
}
