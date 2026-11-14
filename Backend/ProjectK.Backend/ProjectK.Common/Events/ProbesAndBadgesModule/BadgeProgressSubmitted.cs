using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.ProbesAndBadgesModule;

/// <summary>A вмілість was handed in and is waiting to be reviewed.</summary>
public sealed record BadgeProgressSubmitted(
    Guid BadgeProgressKey,
    string BadgeId,
    Guid MemberKey,
    string MemberName,
    Guid KurinKey,
    Guid? GroupKey,
    Guid? ActorUserKey) : IDomainEvent;
