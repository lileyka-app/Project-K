using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>An award was looked at and either stands or does not.</summary>
public sealed record MemberAwardReviewed(
    Guid MemberAwardKey,
    Guid MemberKey,
    Guid MemberUserKey,
    bool IsApproved,
    Guid? ActorUserKey) : IDomainEvent;
