using MediatR;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Services.Events;

/// <summary>
/// Delivers events in the same process, synchronously, while the publisher's request is still
/// running. That is deliberate for now: it keeps behaviour identical to the direct calls it
/// replaced. When delivery has to survive the process, only this class changes — it writes to an
/// outbox instead, and the handlers become broker subscribers.
/// </summary>
public sealed class InProcessDomainEventPublisher : IDomainEventPublisher
{
    private readonly IPublisher _publisher;

    public InProcessDomainEventPublisher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
        => _publisher.Publish(new DomainEventNotification<TEvent>(domainEvent), cancellationToken);
}
