using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>
/// A person was moved from one гурток of a kurin to another, or into or out of one. Carries the old
/// гурток because the membership no longer remembers it.
/// </summary>
public sealed record MembershipMovedToGroup(
    Guid MembershipKey,
    Guid KurinKey,
    Guid? FromGroupKey,
    Guid? ToGroupKey) : IDomainEvent;
