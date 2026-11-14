using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record DuesAccountDto
{
    public required Guid MembershipKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required string FullName { get; init; }
    public required DuesAccountStanding Standing { get; init; }
    public required bool IsConcessionNow { get; init; }

    /// <summary>The kurin's tags on the person that the viewer may see.</summary>
    public required IReadOnlyList<Guid> TagKeys { get; init; }

    /// <summary>What the running quarter costs the person where they stand now; null once they left.</summary>
    public required decimal? RateNow { get; init; }

    /// <summary>The tag whose rate the person pays now, in a kurin without гуртки; null for the rate for everyone.</summary>
    public required Guid? RateTagKey { get; init; }

    /// <summary>The пільги of the край given to the person, newest first.</summary>
    public required IReadOnlyList<DuesConcessionGrantDto> Grants { get; init; }

    public required IReadOnlyList<DuesAccountQuarterDto> Quarters { get; init; }
    public required decimal Charged { get; init; }
    public required decimal Payments { get; init; }
    public required decimal Balance { get; init; }
}
