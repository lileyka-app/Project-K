namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>Everyone carrying a kurin tag, as one target.</summary>
public sealed record AgendaTagTargetDto
{
    public required Guid KurinTagKey { get; set; }
    public required string Name { get; set; }
    public required string ColorHex { get; set; }
}
