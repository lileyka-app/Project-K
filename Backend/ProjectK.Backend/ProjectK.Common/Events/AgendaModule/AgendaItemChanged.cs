using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Events.AgendaModule;

/// <summary>An item people are already assigned to was edited.</summary>
public sealed record AgendaItemChanged(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;
