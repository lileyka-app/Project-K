namespace ProjectK.Common.Dtos.MeModule;

public sealed record MyBadgesDto
{
    /// <summary>Handed in and waiting for the впорядник.</summary>
    public required IReadOnlyList<MyBadgeDto> OnReview { get; init; }
    /// <summary>Begun and not yet handed in, or sent back.</summary>
    public required IReadOnlyList<MyBadgeDto> InWork { get; init; }
    /// <summary>The latest confirmed, newest first.</summary>
    public required IReadOnlyList<MyBadgeDto> Confirmed { get; init; }
    public required int ConfirmedCount { get; init; }
}
