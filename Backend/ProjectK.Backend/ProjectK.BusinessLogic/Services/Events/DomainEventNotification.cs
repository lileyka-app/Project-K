using MediatR;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Services.Events;

/// <summary>
/// Carries a domain event to whoever handles it, wrapped so that MediatR never appears in the
/// publishing module's code — or in <see cref="IDomainEventPublisher"/> itself.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;
