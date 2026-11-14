using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.MeModule;

public sealed record MyProbeDto
{
    public required string ProbeId { get; init; }
    public required string Title { get; init; }
    public required ProbeProgressStatus Status { get; init; }
    public required int SignedPoints { get; init; }
    public required int TotalPoints { get; init; }
    /// <summary>The next few points still to be signed, in the order of the проба.</summary>
    public required IReadOnlyList<MyProbePointDto> NextPoints { get; init; }
}
