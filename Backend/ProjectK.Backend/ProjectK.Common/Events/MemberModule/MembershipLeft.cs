using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>
/// A person's membership in a kurin was closed: released by its Звʼязковий. Whoever counts time in the
/// kurin (the dues ledger) settles the quarter they left in.
/// </summary>
public sealed record MembershipLeft(
    Guid MembershipKey,
    Guid KurinKey,
    DateTime LeftAtUtc) : IDomainEvent;
