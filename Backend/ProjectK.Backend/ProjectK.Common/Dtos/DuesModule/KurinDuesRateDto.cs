namespace ProjectK.Common.Dtos.DuesModule;

public sealed record KurinDuesRateDto
{
    public required QuarterDto FromQuarter { get; init; }

    /// <summary>The tag the rate is for, in a kurin without гуртки; null for everyone else.</summary>
    public required Guid? TagKey { get; init; }

    /// <summary>The kurin's статті, in the order it set them. The amounts below are their totals.</summary>
    public required IReadOnlyList<DuesArticleDto> Articles { get; init; }

    public required decimal StanytsiaFull { get; init; }
    public required decimal StanytsiaReduced { get; init; }
    public required decimal KurinShare { get; init; }
    public required decimal KurinReduced { get; init; }
    public required decimal KraiFull { get; init; }
    public required decimal KraiReduced { get; init; }
}
