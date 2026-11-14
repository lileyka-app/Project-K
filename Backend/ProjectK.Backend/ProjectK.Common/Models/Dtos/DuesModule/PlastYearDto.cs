namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record PlastYearDto
{
    public required int StartYear { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<QuarterDto> Quarters { get; init; }
}
