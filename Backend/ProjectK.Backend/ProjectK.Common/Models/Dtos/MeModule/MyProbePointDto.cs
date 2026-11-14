using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

public sealed record MyProbePointDto
{
    public required string PointId { get; init; }
    public required string SectionCode { get; init; }
    public required string Title { get; init; }
}
