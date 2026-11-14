namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>
/// One person's вкладка in the kurin they are looked at in: what they owe or have over, by гурток
/// and by quarter, and the last operations about them. What a youth sees on their own card.
/// </summary>
public sealed record MemberDuesDto
{
    /// <summary>False when the person has never been charged here — nothing to show.</summary>
    public required bool HasAccount { get; init; }

    public required Guid KurinKey { get; init; }
    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public required string Currency { get; init; }
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public required string NumberLocale { get; init; }
    public required QuarterDto CurrentQuarter { get; init; }

    /// <summary>Across every гурток of this kurin: negative is debt, positive a surplus.</summary>
    public required decimal Balance { get; init; }

    /// <summary>
    /// What keeps the person in the kurin: unpaid quarters already behind them. The running quarter is
    /// not in it — the quarter someone is released in is not charged. Zero when they may be released.
    /// </summary>
    public required decimal OwedToLeave { get; init; }

    /// <summary>What the current quarter costs them in the гурток they stand in now; null when they stand in none.</summary>
    public required DuesAmountDto? QuarterRate { get; init; }
    public required bool IsConcessionNow { get; init; }

    /// <summary>The край's пільги the person holds this quarter.</summary>
    public required IReadOnlyList<string> ConcessionNamesNow { get; init; }

    public required Guid? CurrentGroupKey { get; init; }
    public required string? CurrentGroupName { get; init; }

    /// <summary>Whether the viewer may open the гурток's box — a youth may not, their keepers may.</summary>
    public required bool CanOpenGroupDues { get; init; }

    public required IReadOnlyList<MemberDuesAccountDto> Accounts { get; init; }

    /// <summary>The latest operations about this person, newest first.</summary>
    public required IReadOnlyList<DuesEntryDto> Entries { get; init; }

    /// <summary>
    /// The person's вкладка in their other kurins, one line each. Only on their own card: another
    /// kurin's box is that kurin's business, and its keepers here have no say in it.
    /// </summary>
    public required IReadOnlyList<MemberDuesElsewhereDto> Elsewhere { get; init; }
}
