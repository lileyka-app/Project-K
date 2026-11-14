using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>An award was put forward and is waiting for the провід to look at it.</summary>
public sealed record MemberAwardSubmitted(
    Guid MemberAwardKey,
    Guid MemberKey,
    string MemberName,
    Guid KurinKey,
    Guid? GroupKey,
    Guid? ActorUserKey) : IDomainEvent;
