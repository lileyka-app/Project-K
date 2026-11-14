namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>A гурток's whole box in one read: who owes what, what happened, what is in the box.</summary>
public sealed record GroupDuesDto
{
    public required Guid GroupKey { get; init; }
    public required Guid KurinKey { get; init; }
    public required string GroupName { get; init; }

    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public required string Currency { get; init; }
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public required string NumberLocale { get; init; }
    /// <summary>Where the край's kurins hand the вкладка up: stanytsia, krai or both.</summary>
    public required string Upstream { get; init; }

    /// <summary>The пільги of the край the гурток may give.</summary>
    public required IReadOnlyList<KraiConcessionDto> KraiConcessions { get; init; }

    public required QuarterDto CurrentQuarter { get; init; }

    /// <summary>The пластові роки the table can show, newest first, each with its four quarters.</summary>
    public required IReadOnlyList<PlastYearDto> Years { get; init; }

    public required IReadOnlyList<KurinDuesRateDto> KurinRates { get; init; }
    public required IReadOnlyList<GroupDuesRateDto> GroupRates { get; init; }

    public required IReadOnlyList<DuesAccountDto> Accounts { get; init; }

    /// <summary>The kurin's tags, for naming and filtering the register by them.</summary>
    public required IReadOnlyList<DuesTagDto> Tags { get; init; }
    public required IReadOnlyList<DuesEntryDto> Entries { get; init; }

    public required DuesBoxDto Box { get; init; }
    public required DuesHandoverDto Handover { get; init; }

    /// <summary>Everyone in the kurin, for "who collected" and the like.</summary>
    public required IReadOnlyList<DuesPersonDto> People { get; init; }

    public required DuesViewerDto Viewer { get; init; }
}
