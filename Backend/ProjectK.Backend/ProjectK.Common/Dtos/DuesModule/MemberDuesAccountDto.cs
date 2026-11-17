using ProjectK.Common.Enums.DuesModule;

namespace ProjectK.Common.Dtos.DuesModule;

public sealed record MemberDuesAccountDto
{
    /// <summary>Null for the person's account in the kurin's own box, in a kurin without гуртки.</summary>
    public required Guid? GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required DuesAccountStanding Standing { get; init; }
    public required IReadOnlyList<DuesAccountQuarterDto> Quarters { get; init; }
    public required decimal Charged { get; init; }
    public required decimal Payments { get; init; }
    public required decimal Balance { get; init; }
}
