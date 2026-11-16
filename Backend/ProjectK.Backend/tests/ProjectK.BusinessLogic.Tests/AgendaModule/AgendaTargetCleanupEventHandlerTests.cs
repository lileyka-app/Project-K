using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Events;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

public class AgendaTargetCleanupEventHandlerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAgendaItemRepository> _items = new();
    private readonly Mock<IOfficeDirectory> _offices = new();
    private readonly AgendaTargetCleanupEventHandler _handler;

    public AgendaTargetCleanupEventHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_items.Object);
        _handler = new AgendaTargetCleanupEventHandler(_uow.Object, _offices.Object);
    }

    [Fact]
    public async Task ARemovedPerson_IsNoLongerATarget()
    {
        var person = Guid.NewGuid();

        await _handler.Handle(new DomainEventNotification<MembersRemoved>(new MembersRemoved([person])), default);

        _items.Verify(r => r.RemoveAssignmentsForTargetsAsync(It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { person })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADissolvedGroup_TakesItsProvidWithIt_ButNoOtherOffice()
    {
        var groupProvid = Guid.NewGuid();
        _offices.Setup(o => o.GetLeadershipsInKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LeadershipRef(groupProvid, LeadershipType.Group, Group), new LeadershipRef(Guid.NewGuid(), LeadershipType.KV, null)]);

        await _handler.Handle(new DomainEventNotification<GroupDissolved>(new GroupDissolved(Kurin, Group, DateTime.UtcNow)), default);

        _items.Verify(r => r.RemoveAssignmentsForTargetsAsync(It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { Group, groupProvid })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARemovedTag_IsNoLongerATarget()
    {
        var tag = Guid.NewGuid();

        await _handler.Handle(new DomainEventNotification<KurinTagRemoved>(new KurinTagRemoved(Kurin, tag)), default);

        _items.Verify(r => r.RemoveAssignmentsForTargetsAsync(It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { tag })), It.IsAny<CancellationToken>()), Times.Once);
    }
}
