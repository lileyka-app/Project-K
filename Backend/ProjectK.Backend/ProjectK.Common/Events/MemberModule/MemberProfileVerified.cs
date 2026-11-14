using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>A profile was checked against the person and confirmed current.</summary>
public sealed record MemberProfileVerified(
    Guid MemberKey,
    Guid MemberUserKey,
    Guid? ActorUserKey) : IDomainEvent;
