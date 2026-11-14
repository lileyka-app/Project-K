namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record QuarterDto
{
    public required int Year { get; init; }
    public required int Number { get; init; }
}
