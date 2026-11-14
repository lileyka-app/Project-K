namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record DuesAmountDto
{
    public required decimal Stanytsia { get; init; }
    public required decimal Krai { get; init; }
    public required decimal Kurin { get; init; }
    public required decimal Group { get; init; }
    public required decimal Total { get; init; }
}
