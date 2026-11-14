namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>
/// What the asking kurin may offer a person and who will answer: the kurins to choose from, or the
/// person themself, or nobody when they are taken in at once.
/// </summary>
public sealed record MembershipAskOfferDto(
    bool SeniorKurin,
    bool JoinsAtOnce,
    bool PersonAnswers,
    bool TransferAllowed,
    IReadOnlyList<MembershipRequestKurinDto> AskKurins);
