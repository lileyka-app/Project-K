namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record GroupDuesRateDto
{
    public required QuarterDto FromQuarter { get; init; }
    public required decimal GroupShare { get; init; }
}
