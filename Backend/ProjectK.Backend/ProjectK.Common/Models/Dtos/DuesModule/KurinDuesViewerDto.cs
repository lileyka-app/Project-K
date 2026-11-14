namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record KurinDuesViewerDto
{
    /// <summary>May write the kurin's own operations and confirm transfers — the курінний скарбник and the Звʼязковий.</summary>
    public required bool CanKeep { get; init; }

    /// <summary>May mark a kurin operation verified — the Звʼязковий.</summary>
    public required bool CanVerify { get; init; }

    public required bool CanSetRates { get; init; }
}
