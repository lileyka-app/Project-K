namespace ProjectK.Common.Dtos.DuesModule;

public sealed record KraiConcessionReportRowDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int People { get; init; }

    /// <summary>How much lower each part came out than without the край's пільги.</summary>
    public required DuesAmountDto Reduction { get; init; }

    public required IReadOnlyList<KraiConcessionReportKurinDto> Kurins { get; init; }
}
