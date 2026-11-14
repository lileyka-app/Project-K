using ProjectK.BusinessLogic.Modules.MemberModule.Events;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Events.KurinModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// Delivers the kurin's structural events to the member module's handler on the same unit of work, as
/// the in-process publisher does, so a kurin handler's test sees what its deletion clears.
/// </summary>
public sealed class KurinCleanupEvents : IDomainEventPublisher
{
    private readonly KurinCleanupEventHandler _handler;

    public KurinCleanupEvents(IUnitOfWork unitOfWork)
    {
        _handler = new KurinCleanupEventHandler(unitOfWork);
    }

    public List<IDomainEvent> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        Published.Add(domainEvent);
        return domainEvent switch
        {
            KurinDeleted deleted => _handler.Handle(new DomainEventNotification<KurinDeleted>(deleted), cancellationToken),
            GroupDissolved dissolved => _handler.Handle(new DomainEventNotification<GroupDissolved>(dissolved), cancellationToken),
            _ => Task.CompletedTask,
        };
    }
}
