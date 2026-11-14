namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>What one гурток's youth owe up for one quarter, and how far they have paid it.</summary>
public sealed record KurinDuesQuarterGroupDto
{
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }

    /// <summary>Youth charged for the quarter in this гурток.</summary>
    public required int YouthCount { get; init; }

    /// <summary>The станиця, край and kurin parts the гурток has to hand up for the quarter.</summary>
    public required decimal ExpectedUp { get; init; }

    /// <summary>Of that, what the youth have paid so far.</summary>
    public required decimal CollectedUp { get; init; }

    /// <summary>What the youth still owe of it.</summary>
    public required decimal DebtUp { get; init; }

    /// <summary>The станиця part alone: what the kurin owes the станиця for this quarter's youth.</summary>
    public required decimal StanytsiaExpected { get; init; }
    public required decimal StanytsiaCollected { get; init; }

    /// <summary>The край part alone: what the kurin owes the край for this quarter's youth.</summary>
    public required decimal KraiExpected { get; init; }
    public required decimal KraiCollected { get; init; }
}
