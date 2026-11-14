using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>An item was removed, and the people it was assigned to should stop expecting it.</summary>
public sealed record AgendaItemRemoved(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;
