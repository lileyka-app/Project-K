using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>One invitation as the person reads it: who asks, and how they would stand there.</summary>
public sealed record MembershipInvitationDto(
    Guid MembershipRequestKey,
    MembershipRequestKurinDto Kurin,
    MembershipKind MembershipKind,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);
