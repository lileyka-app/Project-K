namespace ProjectK.Common.Models.Dtos.DuesModule;

/// <summary>A пільга the край offers: what the гурток may give.</summary>
public sealed record KraiConcessionDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required decimal Value { get; init; }
    public required IReadOnlyList<string> AppliesTo { get; init; }
}
