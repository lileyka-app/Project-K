using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>A пересторога was written against a member.</summary>
public sealed record MemberWarningAssigned(
    Guid MemberWarningKey,
    Guid MemberKey,
    Guid MemberUserKey,
    MemberWarningLevel Level,
    Guid? ActorUserKey) : IDomainEvent;
