using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Events;

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

/// <summary>An item people are already assigned to was edited.</summary>
public sealed record AgendaItemChanged(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;

/// <summary>An item was removed, and the people it was assigned to should stop expecting it.</summary>
public sealed record AgendaItemRemoved(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    IReadOnlyCollection<Guid> RecipientUserKeys,
    Guid ActorUserKey) : IDomainEvent;

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
