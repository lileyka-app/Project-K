namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// What the asking kurin may offer a person and who will answer: the kurins to choose from, or the
/// person themself, or nobody when they are taken in at once. <paramref name="StillYouth"/>: a senior kurin
/// may not ask at all, the person is still in УПЮ.
/// </summary>
public sealed record MembershipAskOfferDto(
    bool SeniorKurin,
    bool JoinsAtOnce,
    bool PersonAnswers,
    bool TransferAllowed,
    IReadOnlyList<MembershipRequestKurinDto> AskKurins,
    bool StillYouth = false);
