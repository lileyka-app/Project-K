using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Models;

/// <summary>
/// The kurin's side of the вкладка in one read: its own box, what each гурток owes up and has handed
/// over, the transfers waiting to be confirmed, and its own operations.
/// </summary>
public sealed class KurinDuesResponse
{
    public Guid KurinKey { get; init; }
    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public string Currency { get; init; } = "UAH";
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public string NumberLocale { get; init; } = "uk-UA";
    /// <summary>Where the край's kurins hand the вкладка up: stanytsia, krai or both.</summary>
    public string Upstream { get; init; } = KraiDues.Stanytsia;
    public QuarterDto CurrentQuarter { get; init; } = new();
    public IReadOnlyList<PlastYearDto> Years { get; init; } = [];
    public IReadOnlyList<KurinDuesRateDto> Rates { get; init; } = [];

    /// <summary><c>ToForward</c> here is what still has to go to the станиця and the край; <c>InTransit</c> — handed over by гуртки, not yet confirmed.</summary>
    public DuesBoxDto Box { get; init; } = new();

    /// <summary>Of <c>Box.ToForward</c>, what is the станиця's.</summary>
    public decimal ToStanytsia { get; init; }

    /// <summary>Of <c>Box.ToForward</c>, what is the край's.</summary>
    public decimal ToKrai { get; init; }

    public decimal SentToStanytsia { get; init; }
    public decimal SentToKrai { get; init; }

    public IReadOnlyList<KurinGroupHandoverDto> Groups { get; init; } = [];
    public IReadOnlyList<DuesTransferDto> Transfers { get; init; } = [];

    /// <summary>
    /// Quarter by quarter, what every гурток owes up for its youth and what of it the youth have paid —
    /// the kurin's view of the same table each гурток keeps. Oldest quarter first.
    /// </summary>
    public IReadOnlyList<KurinDuesQuarterDto> Quarters { get; init; } = [];

    /// <summary>The kurin's own operations — not the гуртки's.</summary>
    public IReadOnlyList<DuesEntryDto> Entries { get; init; } = [];

    public IReadOnlyList<DuesPersonDto> People { get; init; } = [];
    public KurinDuesViewerDto Viewer { get; init; } = new();
}

/// <summary>One quarter across the гуртки, with the kurin's total as one more row.</summary>
public sealed class KurinDuesQuarterDto
{
    public QuarterDto Quarter { get; init; } = new();
    public IReadOnlyList<KurinDuesQuarterGroupDto> Groups { get; init; } = [];
    public KurinDuesQuarterGroupDto Total { get; init; } = new();
}

/// <summary>What one гурток's youth owe up for one quarter, and how far they have paid it.</summary>
public sealed class KurinDuesQuarterGroupDto
{
    public Guid GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;

    /// <summary>Youth charged for the quarter in this гурток.</summary>
    public int YouthCount { get; init; }

    /// <summary>The станиця, край and kurin parts the гурток has to hand up for the quarter.</summary>
    public decimal ExpectedUp { get; init; }

    /// <summary>Of that, what the youth have paid so far.</summary>
    public decimal CollectedUp { get; init; }

    /// <summary>What the youth still owe of it.</summary>
    public decimal DebtUp { get; init; }

    /// <summary>The станиця part alone: what the kurin owes the станиця for this quarter's youth.</summary>
    public decimal StanytsiaExpected { get; init; }
    public decimal StanytsiaCollected { get; init; }

    /// <summary>The край part alone: what the kurin owes the край for this quarter's youth.</summary>
    public decimal KraiExpected { get; init; }
    public decimal KraiCollected { get; init; }
}

/// <summary>A гурток whose box the caller may open.</summary>
public sealed class DuesGroupLinkDto
{
    public Guid GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;
}

/// <summary>How one гурток stands with the kurin.</summary>
public sealed class KurinGroupHandoverDto
{
    public Guid GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;

    /// <summary>Collected by the гурток for the станиця, the край and the kurin so far.</summary>
    public decimal OwedUp { get; init; }
    public decimal Transferred { get; init; }
    public decimal Received { get; init; }

    /// <summary>Collected but not handed over yet — still in the гурток's box.</summary>
    public decimal Outstanding { get; init; }

    /// <summary>Handed over, not yet confirmed by the kurin.</summary>
    public decimal InTransit { get; init; }
}

/// <summary>A гурток's transfer to the kurin, as the kurin sees it.</summary>
public sealed class DuesTransferDto
{
    public Guid DuesEntryKey { get; init; }
    public Guid GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DuesPaymentMethod Method { get; init; }
    public DateOnly OccurredOn { get; init; }
    public string? CollectedByName { get; init; }
    public string? Note { get; init; }
    public bool IsReceived { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public string? ReceivedByName { get; init; }
}

public sealed class KurinDuesViewerDto
{
    /// <summary>May write the kurin's own operations and confirm transfers — the курінний скарбник and the Звʼязковий.</summary>
    public bool CanKeep { get; init; }

    /// <summary>May mark a kurin operation verified — the Звʼязковий.</summary>
    public bool CanVerify { get; init; }

    public bool CanSetRates { get; init; }
}
