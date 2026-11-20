namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>One quarter across the гуртки, with the kurin's total as one more row.</summary>
public sealed record KurinDuesQuarterDto
{
    public required QuarterDto Quarter { get; init; }
    public required IReadOnlyList<KurinDuesQuarterGroupDto> Groups { get; init; }
    public required KurinDuesQuarterGroupDto Total { get; init; }
}
