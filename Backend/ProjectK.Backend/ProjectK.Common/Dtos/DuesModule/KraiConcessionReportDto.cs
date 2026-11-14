namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>What the край's пільги took off the вкладка of its kurins in one quarter.</summary>
public sealed record KraiConcessionReportDto
{
    public required string KraiCode { get; init; }
    public required string KraiName { get; init; }
    public required string Currency { get; init; }
    public required string NumberLocale { get; init; }
    public required QuarterDto Quarter { get; init; }
    public required int KurinCount { get; init; }
    public required IReadOnlyList<KraiConcessionReportRowDto> Concessions { get; init; }
}
