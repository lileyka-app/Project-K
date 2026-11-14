using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Events.MemberModule;

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
    Guid? FromKurinKey,
    Guid ToKurinKey,
    bool Approved,
    Guid? ActorUserKey) : IDomainEvent;
