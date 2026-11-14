namespace ProjectK.Common.Models.Dtos.DuesModule;

/// <summary>What the person reading may do here, so the screen offers only that.</summary>
public sealed record DuesViewerDto
{
    public required bool CanKeep { get; init; }
    public required bool CanVerify { get; init; }

    /// <summary>Whether they may set the станиця and kurin parts — the Звʼязковий and the курінний скарбник.</summary>
    public required bool CanSetKurinRates { get; init; }
}
