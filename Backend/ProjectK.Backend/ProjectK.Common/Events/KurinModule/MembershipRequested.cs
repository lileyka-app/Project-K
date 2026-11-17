using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A kurin asked the kurin a person stands in to let them go, or to let them stand in both. News for
/// the Звʼязковий who answers it.
/// </summary>
public sealed record MembershipRequested(
    Guid MembershipRequestKey,
    MembershipRequestKind Kind,
    Guid MemberKey,
    Guid? MemberUserKey,
    string MemberName,
    Guid? FromKurinKey,
    Guid ToKurinKey,
    Guid? ActorUserKey) : IDomainEvent;
