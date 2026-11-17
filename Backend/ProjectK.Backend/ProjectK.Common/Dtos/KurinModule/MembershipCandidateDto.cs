
namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// The card, whether this kurin already has them, and the kurins they stand in now. Those kurins are
/// named only by number: the person handed over their code so this kurin could ask for them, and
/// asking means asking a particular kurin — «перевести» from it, or «прийняти ще й до нас» alongside it.
/// No contact details, and nothing about kurins they once left.
/// </summary>
public sealed record MembershipCandidateDto(
    MembershipCandidateMemberDto Member,
    bool AlreadyInThisKurin,
    IReadOnlyList<MembershipRequestKurinDto> CurrentKurins,
    MembershipAskOfferDto Offer);
