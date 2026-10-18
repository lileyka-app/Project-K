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

    /// <summary>What the current quarter costs them in the гурток they stand in now; null when they stand in none.</summary>
    public DuesAmountDto? QuarterRate { get; init; }
    public bool IsConcessionNow { get; init; }

    public Guid? CurrentGroupKey { get; init; }
    public string? CurrentGroupName { get; init; }

    /// <summary>Whether the viewer may open the гурток's box — a youth may not, their keepers may.</summary>
    public bool CanOpenGroupDues { get; init; }

    public IReadOnlyList<MemberDuesAccountDto> Accounts { get; init; } = [];

    /// <summary>The latest operations about this person, newest first.</summary>
    public IReadOnlyList<DuesEntryDto> Entries { get; init; } = [];
}

public sealed class MemberDuesAccountDto
{
    public Guid GroupKey { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public DuesAccountStanding Standing { get; init; }
    public IReadOnlyList<DuesAccountQuarterDto> Quarters { get; init; } = [];
    public decimal Charged { get; init; }
    public decimal Payments { get; init; }
    public decimal Balance { get; init; }
}
