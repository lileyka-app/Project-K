using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Models;

/// <summary>
/// One person's вкладка in the kurin they are looked at in: what they owe or have over, by гурток
/// and by quarter, and the last operations about them. What a youth sees on their own card.
/// </summary>
public sealed class MemberDuesResponse
{
    /// <summary>False when the person has never been charged here — nothing to show.</summary>
    public bool HasAccount { get; init; }

    public Guid KurinKey { get; init; }
    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public string Currency { get; init; } = "UAH";
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public string NumberLocale { get; init; } = "uk-UA";
    public QuarterDto CurrentQuarter { get; init; } = new();

    /// <summary>Across every гурток of this kurin: negative is debt, positive a surplus.</summary>
    public decimal Balance { get; init; }

    /// <summary>
    /// What keeps the person in the kurin: unpaid quarters already behind them. The running quarter is
    /// not in it — the quarter someone is released in is not charged. Zero when they may be released.
    /// </summary>
    public decimal OwedToLeave { get; init; }

    /// <summary>What the current quarter costs them in the гурток they stand in now; null when they stand in none.</summary>
    public DuesAmountDto? QuarterRate { get; init; }
    public bool IsConcessionNow { get; init; }

    /// <summary>The край's пільги the person holds this quarter.</summary>
    public IReadOnlyList<string> ConcessionNamesNow { get; init; } = [];

    public Guid? CurrentGroupKey { get; init; }
    public string? CurrentGroupName { get; init; }

    /// <summary>Whether the viewer may open the гурток's box — a youth may not, their keepers may.</summary>
    public bool CanOpenGroupDues { get; init; }

    public IReadOnlyList<MemberDuesAccountDto> Accounts { get; init; } = [];

    /// <summary>The latest operations about this person, newest first.</summary>
    public IReadOnlyList<DuesEntryDto> Entries { get; init; } = [];

    /// <summary>
    /// The person's вкладка in their other kurins, one line each. Only on their own card: another
    /// kurin's box is that kurin's business, and its keepers here have no say in it.
    /// </summary>
    public IReadOnlyList<MemberDuesElsewhereDto> Elsewhere { get; init; } = [];
}

/// <summary>What the person owes or has over in one of their other kurins, named by their own line there.</summary>
public sealed class MemberDuesElsewhereDto
{
    public Guid KurinKey { get; init; }
    public int? KurinNumber { get; init; }
    public string? KurinNamedAfter { get; init; }
    /// <summary>The person's own line in that kurin: УПС for a сеніор in a kurin of both senior гілки.</summary>
    public KurinBranch Branch { get; init; }
    public IReadOnlyList<KurinNumbering> Numbering { get; init; } = [];
    public string Currency { get; init; } = "UAH";
    public string NumberLocale { get; init; } = "uk-UA";
    /// <summary>Negative is debt, positive a surplus.</summary>
    public decimal Balance { get; init; }
}

public sealed class MemberDuesAccountDto
{
    /// <summary>Null for the person's account in the kurin's own box, in a kurin without гуртки.</summary>
    public Guid? GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public DuesAccountStanding Standing { get; init; }
    public IReadOnlyList<DuesAccountQuarterDto> Quarters { get; init; } = [];
    public decimal Charged { get; init; }
    public decimal Payments { get; init; }
    public decimal Balance { get; init; }
}
