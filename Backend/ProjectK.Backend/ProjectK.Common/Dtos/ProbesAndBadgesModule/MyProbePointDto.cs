namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

public sealed record MyProbePointDto
{
    public required string PointId { get; init; }
    public required string SectionCode { get; init; }
    public required string Title { get; init; }
}
