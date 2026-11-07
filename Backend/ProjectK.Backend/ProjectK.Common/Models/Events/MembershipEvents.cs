using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Events;

/// <summary>
/// A person was moved from one гурток of a kurin to another, or into or out of one. Carries the old
/// гурток because the membership no longer remembers it.
/// </summary>
public sealed record MembershipMovedToGroup(
    Guid MembershipKey,
    Guid KurinKey,
    Guid? FromGroupKey,
    Guid? ToGroupKey) : IDomainEvent;

/// <summary>
/// A person's membership in a kurin was closed: released by its Звʼязковий. Whoever counts time in the
/// kurin (the dues ledger) settles the quarter they left in.
/// </summary>
public sealed record MembershipLeft(
    Guid MembershipKey,
    Guid KurinKey,
    DateTime LeftAtUtc) : IDomainEvent;

/// <summary>
/// A kurin asked the kurin a person stands in to let them go, or to let them stand in both. News for
/// the Звʼязковий who answers it.
/// </summary>
public sealed record MembershipRequested(
    Guid MembershipRequestKey,
    MembershipRequestKind Kind,
    Guid MemberKey,
    string MemberName,
    Guid FromKurinKey,
    Guid ToKurinKey,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>
/// A request between kurins was answered. The asking kurin hears either way; the person hears when it
/// was approved — they now stand somewhere new.
/// </summary>
public sealed record MembershipRequestDecided(
    Guid MembershipRequestKey,
    MembershipRequestKind Kind,
    Guid MemberKey,
    Guid? MemberUserKey,
    string MemberName,
    Guid FromKurinKey,
    Guid ToKurinKey,
    bool Approved,
    Guid? ActorUserKey) : IDomainEvent;
