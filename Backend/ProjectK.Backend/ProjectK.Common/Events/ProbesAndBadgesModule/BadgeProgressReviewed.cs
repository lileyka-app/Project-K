using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.ProbesAndBadgesModule;

/// <summary>
/// A вмілість was reviewed. <paramref name="ConfirmationWithdrawn"/> separates "not accepted yet"
/// from "an earlier confirmation was taken back", which read the same to the member otherwise.
/// </summary>
public sealed record BadgeProgressReviewed(
    Guid BadgeProgressKey,
    string BadgeId,
    Guid MemberKey,
    Guid MemberUserKey,
    bool IsApproved,
    bool ConfirmationWithdrawn,
    Guid? ActorUserKey) : IDomainEvent;
