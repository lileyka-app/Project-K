namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// What the asking kurin may offer a person and who will answer: the kurins to choose from, or the
/// person themself, or nobody when they are taken in at once. <paramref name="StillYouth"/>: a senior kurin
/// may not ask at all, the person is still in УПЮ. <paramref name="AlongsideAllowed"/>: «прийняти ще й до нас»
/// is offered; someone who already stands in a senior kurin moves to another one only by «перевести».
/// </summary>
public sealed record MembershipAskOfferDto(
    bool SeniorKurin,
    bool JoinsAtOnce,
    bool PersonAnswers,
    bool TransferAllowed,
    IReadOnlyList<MembershipRequestKurinDto> AskKurins,
    bool StillYouth = false,
    bool AlongsideAllowed = true);
