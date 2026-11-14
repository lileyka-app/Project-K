using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A task moved on the board. Only the person who raised it is told, so the event names them rather
/// than a list.
/// </summary>
public sealed record AgendaItemStatusChanged(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    AgendaItemStatus Status,
    Guid CreatorUserKey,
    Guid ActorUserKey) : IDomainEvent;
