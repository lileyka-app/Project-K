namespace ProjectK.Common.Dtos.MeModule;

public sealed record MyProbePointDto
{
    public required string PointId { get; init; }
    public required string SectionCode { get; init; }
    public required string Title { get; init; }
}
