using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>The person's вкладка in one kurin: what they owe or have over, and what this quarter costs.</summary>
public sealed record MyDuesDto
{
    public required MyKurinRefDto Kurin { get; init; }
    public required string? GroupName { get; init; }
    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public required string Currency { get; init; }
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public required string NumberLocale { get; init; }
    public required int QuarterYear { get; init; }
    public required int QuarterNumber { get; init; }
    /// <summary>Across every гурток of the kurin: negative is debt, positive a surplus.</summary>
    public required decimal Balance { get; init; }
    /// <summary>What the running quarter costs them; null when they stand in no гурток.</summary>
    public required decimal? QuarterRate { get; init; }
    public required bool IsConcession { get; init; }

    /// <summary>The край's пільги the person holds this quarter.</summary>
    public required IReadOnlyList<string> ConcessionNames { get; init; }
}
