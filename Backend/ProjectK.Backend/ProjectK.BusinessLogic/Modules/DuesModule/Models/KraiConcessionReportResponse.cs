namespace ProjectK.BusinessLogic.Modules.DuesModule.Models;

/// <summary>What the край's пільги took off the вкладка of its kurins in one quarter.</summary>
public sealed class KraiConcessionReportResponse
{
    public string KraiCode { get; init; } = string.Empty;
    public string KraiName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string NumberLocale { get; init; } = string.Empty;
    public QuarterDto Quarter { get; init; } = new();
    public int KurinCount { get; init; }
    public IReadOnlyList<KraiConcessionReportRowDto> Concessions { get; init; } = [];
}

public sealed class KraiConcessionReportRowDto
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int People { get; init; }

    /// <summary>How much lower each part came out than without the край's пільги.</summary>
    public DuesAmountDto Reduction { get; init; } = new();

    public IReadOnlyList<KraiConcessionReportKurinDto> Kurins { get; init; } = [];
}

public sealed class KraiConcessionReportKurinDto
{
    public Guid KurinKey { get; init; }
    public int? Number { get; init; }
    public int People { get; init; }
    public decimal Reduction { get; init; }
}
