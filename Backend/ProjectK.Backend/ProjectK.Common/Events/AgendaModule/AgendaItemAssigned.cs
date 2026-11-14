using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Events.AgendaModule;

/// <summary>
/// The people an agenda change reaches, worked out from the item's assignments. Unlike a review,
/// where "who should hear this" is a question about the kurin's провід, this list can only be built
/// from the item itself — so the agenda states it rather than leaving it to the listener.
/// </summary>
public sealed record AgendaItemAssigned(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;
