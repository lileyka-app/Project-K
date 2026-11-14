using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Events.AgendaModule;

/// <summary>An item was removed, and the people it was assigned to should stop expecting it.</summary>
public sealed record AgendaItemRemoved(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;
