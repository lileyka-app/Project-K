namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// How a module announces what happened without knowing who cares. Today the announcement is
/// delivered in the same process and inside the same request; the contract says nothing about that,
/// so the delivery can become a message broker without any publisher changing.
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}
