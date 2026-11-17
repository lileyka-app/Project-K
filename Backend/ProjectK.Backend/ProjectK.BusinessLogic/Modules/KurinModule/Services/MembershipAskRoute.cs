namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Who answers when a kurin asks for a person, and what it may ask. Youth and senior kurins are two
/// separate lines: a kurin of the same line as the asking one answers, as between any two kurins.
/// Across lines nothing is closed — only «прийняти ще й до нас» — and the person answers for
/// themselves, unless they are still a юнак of an УПЮ kurin: then that kurin answers, so a юнак never
/// puts themselves in УСП or УПС.
/// </summary>
/// <param name="ToSenior">The asking kurin is УСП/УПС: it takes people in only as members, never as юнаки or впорядники.</param>
/// <param name="JoinsAtOnce">Nobody to ask: the person stands in no kurin and has no account to answer from.</param>
/// <param name="PersonAnswers">The request goes to the person, not to a kurin.</param>
/// <param name="TransferAllowed">«Перевести» is possible: only between kurins of the same line.</param>
/// <param name="AskableKurinKeys">The kurins whose Звʼязковий may answer; one is chosen when there are several.</param>
public sealed record MembershipAskRoute(
    bool ToSenior,
    bool JoinsAtOnce,
    bool PersonAnswers,
    bool TransferAllowed,
    IReadOnlyList<Guid> AskableKurinKeys);
