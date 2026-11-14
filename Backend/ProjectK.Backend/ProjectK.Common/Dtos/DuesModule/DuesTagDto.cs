namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>A tag of the kurin, as the dues screens name and colour it.</summary>
public sealed record DuesTagDto
{
    public required Guid TagKey { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
}
