using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>One invitation as the person reads it: who asks, and how they would stand there.</summary>
public sealed record MembershipInvitationDto(
    Guid MembershipRequestKey,
    MembershipRequestKurinDto Kurin,
    MembershipKind MembershipKind,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);
