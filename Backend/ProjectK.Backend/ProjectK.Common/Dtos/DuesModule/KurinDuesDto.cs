using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>
/// The kurin's side of the вкладка in one read: its own box, what each гурток owes up and has handed
/// over, the transfers waiting to be confirmed, and its own operations.
/// </summary>
public sealed record KurinDuesDto
{
    public required Guid KurinKey { get; init; }
    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public required string Currency { get; init; }
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public required string NumberLocale { get; init; }
    /// <summary>Where the край's kurins hand the вкладка up: stanytsia, krai or both.</summary>
    public required string Upstream { get; init; }
    public required QuarterDto CurrentQuarter { get; init; }

    /// <summary>When the вкладка falls due: наперед (the running quarter is owed) or за пройдений квартал.</summary>
    public required DuesChargeTiming ChargeTiming { get; init; }
    public required IReadOnlyList<PlastYearDto> Years { get; init; }
    public required IReadOnlyList<KurinDuesRateDto> Rates { get; init; }

    /// <summary><c>ToForward</c> here is what still has to go to the станиця and the край; <c>InTransit</c> — handed over by гуртки, not yet confirmed.</summary>
    public required DuesBoxDto Box { get; init; }

    /// <summary>Of <c>Box.ToForward</c>, what is the станиця's.</summary>
    public required decimal ToStanytsia { get; init; }

    /// <summary>Of <c>Box.ToForward</c>, what is the край's.</summary>
    public required decimal ToKrai { get; init; }

    public required decimal SentToStanytsia { get; init; }
    public required decimal SentToKrai { get; init; }

    public required IReadOnlyList<KurinGroupHandoverDto> Groups { get; init; }

    /// <summary>A senior kurin has no гуртки: each person's account is in the kurin's own box.</summary>
    public required bool IsSenior { get; init; }

    /// <summary>The personal accounts in the kurin's own box — the treasurer's register of a senior kurin.</summary>
    public required IReadOnlyList<DuesAccountDto> Accounts { get; init; }

    /// <summary>The kurin's tags, for its rates and for naming and filtering the register.</summary>
    public required IReadOnlyList<DuesTagDto> Tags { get; init; }

    /// <summary>The пільги of the край the kurin may give, for its own accounts.</summary>
    public required IReadOnlyList<KraiConcessionDto> KraiConcessions { get; init; }

    public required IReadOnlyList<DuesTransferDto> Transfers { get; init; }

    /// <summary>
    /// Quarter by quarter, what every гурток owes up for its youth and what of it the youth have paid —
    /// the kurin's view of the same table each гурток keeps. Oldest quarter first.
    /// </summary>
    public required IReadOnlyList<KurinDuesQuarterDto> Quarters { get; init; }

    /// <summary>The kurin's own operations — not the гуртки's.</summary>
    public required IReadOnlyList<DuesEntryDto> Entries { get; init; }

    public required IReadOnlyList<DuesPersonDto> People { get; init; }
    public required KurinDuesViewerDto Viewer { get; init; }
}
