using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>An item people are already assigned to was edited.</summary>
public sealed record AgendaItemChanged(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;
