using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Leave;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MembershipHandlers;

/// <summary>
/// Debt does not leave with the person: the kurin releases someone only once they owe it nothing for
/// quarters already behind them, counting every stretch of theirs in that kurin and none elsewhere.
/// </summary>
public class LeaveKurinDuesTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IDuesDirectory> _dues = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly JournalRecorder _journal = new(Mock.Of<ICurrentUserContext>());

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _otherKurinKey = Guid.NewGuid();
    private readonly Membership _membership;
    private readonly Guid _earlierStretchKey = Guid.NewGuid();
    private readonly Guid _elsewhereKey = Guid.NewGuid();

    public LeaveKurinDuesTests()
    {
        _membership = new Membership { MemberKey = Guid.NewGuid(), KurinKey = _kurinKey, JoinedAtUtc = DateTime.UtcNow.AddYears(-1) };

        _unitOfWork.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        _unitOfWork.SetupGet(u => u.Groups).Returns(Mock.Of<IGroupRepository>());
        _unitOfWork.SetupGet(u => u.MembershipRequests).Returns(NoMembershipRequests.Repository());
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _memberships.Setup(r => r.GetActiveAsync(_membership.MemberKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_membership);
        _memberships.Setup(r => r.GetRecordsForMemberAsync(_membership.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Stretch(_membership.MembershipKey, _kurinKey, null),
                Stretch(_earlierStretchKey, _kurinKey, DateTime.UtcNow.AddYears(-2)),
                Stretch(_elsewhereKey, _otherKurinKey, null),
            ]);
    }

    private static MembershipRecord Stretch(Guid key, Guid kurinKey, DateTime? leftAtUtc) =>
        new(key, kurinKey, 1, KurinBranch.UPYu, null, null, null, MembershipKind.Youth, DateTime.UtcNow.AddYears(-3), leftAtUtc);

    private LeaveKurinCommandHandler Handler() =>
        new(_unitOfWork.Object, Mock.Of<IAccessChangeSignal>(), Mock.Of<IMemberDirectory>(), new MembershipMoves(_unitOfWork.Object, _journal.Audit, _events.Object, _dues.Object, TimeProvider.System));

    private void Owes(decimal amount) =>
        _dues.Setup(d => d.GetOwedAsync(_kurinKey, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(amount);

    [Fact]
    public async Task SomeoneWhoOwes_StaysAndNothingIsRecorded()
    {
        Owes(150m);

        var result = await Handler().Handle(new LeaveKurinCommand(_membership.MemberKey, _kurinKey), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("MemberOwesDues");
        _membership.LeftAtUtc.Should().BeNull();
        _journal.Entries.Should().BeEmpty();
        _events.Verify(e => e.PublishAsync(It.IsAny<MembershipLeft>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SomeoneSquare_IsReleased()
    {
        Owes(0m);

        var result = await Handler().Handle(new LeaveKurinCommand(_membership.MemberKey, _kurinKey), default);

        result.Type.Should().Be(ResultType.Success);
        _membership.LeftAtUtc.Should().NotBeNull();
        // The dues module drops the charge for the quarter they leave in.
        _events.Verify(e => e.PublishAsync(
            It.Is<MembershipLeft>(l => l.MembershipKey == _membership.MembershipKey && l.KurinKey == _kurinKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheDebtAsked_IsEveryStretchInThisKurin_AndNoneElsewhere()
    {
        IReadOnlyCollection<Guid>? asked = null;
        _dues.Setup(d => d.GetOwedAsync(_kurinKey, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<Guid>, CancellationToken>((_, keys, _) => asked = keys)
            .ReturnsAsync(0m);

        await Handler().Handle(new LeaveKurinCommand(_membership.MemberKey, _kurinKey), default);

        asked.Should().BeEquivalentTo([_membership.MembershipKey, _earlierStretchKey]);
    }
}
