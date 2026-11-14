using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// A former member and the kurins they stand in now, named only by number. Empty when they stand
/// nowhere: then the kurin takes them back without asking anyone.
/// </summary>
public sealed record FormerMemberStandingDto(FormerMember Former, IReadOnlyList<MembershipRequestKurinDto> StandsIn, MembershipAskOfferDto Offer);
