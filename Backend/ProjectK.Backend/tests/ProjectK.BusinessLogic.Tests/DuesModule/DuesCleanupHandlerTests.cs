using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>
/// A removed person's вкладка is closed, not left as a nameless row: the kurin module says which
/// memberships went, the dues module drops their charges and пільги and marks their money deleted.
/// </summary>
public class DuesCleanupHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MembershipCleanup_TellsTheRestWhichMembershipsWent()
    {
        var memberKey = Guid.NewGuid();
        var dropped = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var memberships = new Mock<IMembershipRepository>();
        memberships
            .Setup(r => r.RemoveForMembersAsync(It.Is<IReadOnlyCollection<Guid>>(k => k.Contains(memberKey)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dropped);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Memberships).Returns(memberships.Object);
        var events = new Mock<IDomainEventPublisher>();

        await new MembershipCleanupEventHandler(unitOfWork.Object, events.Object)
            .Handle(new DomainEventNotification<MembersRemoved>(new MembersRemoved([memberKey])), CancellationToken.None);

        events.Verify(e => e.PublishAsync(
            It.Is<MembershipsRemoved>(r => r.MembershipKeys.SequenceEqual(dropped)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MembershipCleanup_SaysNothing_WhenNothingWasDropped()
    {
        var memberships = new Mock<IMembershipRepository>();
        memberships
            .Setup(r => r.RemoveForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Memberships).Returns(memberships.Object);
        var events = new Mock<IDomainEventPublisher>();

        await new MembershipCleanupEventHandler(unitOfWork.Object, events.Object)
            .Handle(new DomainEventNotification<MembersRemoved>(new MembersRemoved([Guid.NewGuid()])), CancellationToken.None);

        events.Verify(e => e.PublishAsync(It.IsAny<MembershipsRemoved>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DuesCleanup_DropsChargesAndConcessions_AndMarksTheMoneyDeletedWithATrail()
    {
        var membershipKey = Guid.NewGuid();
        var charges = new Mock<IDuesChargeRepository>();
        var concessions = new Mock<IDuesConcessionRepository>();
        var grants = new Mock<IDuesConcessionGrantRepository>();
        var entries = new Mock<IDuesEntryRepository>();
        var payment = new DuesEntry { MembershipKey = membershipKey, Amount = 50, Kind = DuesEntryKind.Contribution };
        entries
            .Setup(r => r.GetForMembershipsAsync(It.Is<IReadOnlyCollection<Guid>>(k => k.Contains(membershipKey)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);
        var dues = new Mock<IDuesUnitOfWork>();
        dues.SetupGet(d => d.DuesCharges).Returns(charges.Object);
        dues.SetupGet(d => d.DuesConcessions).Returns(concessions.Object);
        dues.SetupGet(d => d.DuesConcessionGrants).Returns(grants.Object);
        dues.SetupGet(d => d.DuesEntries).Returns(entries.Object);

        await new DuesCleanupEventHandler(dues.Object, new FixedTimeProvider(Now))
            .Handle(new DomainEventNotification<MembershipsRemoved>(new MembershipsRemoved([membershipKey])), CancellationToken.None);

        charges.Verify(r => r.DeleteForMembershipsAsync(It.Is<IReadOnlyCollection<Guid>>(k => k.Contains(membershipKey)), It.IsAny<CancellationToken>()), Times.Once);
        concessions.Verify(r => r.DeleteForMembershipsAsync(It.Is<IReadOnlyCollection<Guid>>(k => k.Contains(membershipKey)), It.IsAny<CancellationToken>()), Times.Once);
        grants.Verify(r => r.DeleteForMembershipsAsync(It.Is<IReadOnlyCollection<Guid>>(k => k.Contains(membershipKey)), It.IsAny<CancellationToken>()), Times.Once);
        entries.Verify(r => r.Delete(It.IsAny<DuesEntry>(), It.IsAny<CancellationToken>()), Times.Never, "money is marked, never removed");
        payment.DeletedAtUtc.Should().Be(Now);
        payment.Events.Should().ContainSingle(e => e.Action == DuesEntryTrail.Deleted && e.ActorUserKey == null);
    }
}
