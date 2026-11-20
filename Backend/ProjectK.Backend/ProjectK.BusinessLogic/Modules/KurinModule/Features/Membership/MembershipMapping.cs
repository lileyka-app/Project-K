using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;

/// <summary>How a kurin's membership reads name the people in them.</summary>
public static class MembershipMapping
{
    /// <summary>The card a public code found, with how many kurins the person stands in now.</summary>
    public static MembershipCandidateMemberDto ToCandidateDto(this MemberCard card, int currentMembershipCount)
        => new(
            card.MemberKey,
            card.PublicId,
            card.FirstName,
            card.LastName,
            card.ProfilePhotoBlobName,
            currentMembershipCount,
            card.FullName);
}
