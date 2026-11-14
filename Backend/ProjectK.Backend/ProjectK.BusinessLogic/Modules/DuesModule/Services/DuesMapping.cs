using ProjectK.Common.Dtos.DuesModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Rules.DuesModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>The dues read models as every box shows them: a гурток's, the kurin's and a person's own.</summary>
internal static class DuesMapping
{
    public static QuarterDto ToDto(this DuesQuarter quarter) => new() { Year = quarter.Year, Number = quarter.Number };

    public static DuesAmountDto ToDto(this DuesAmount amount) => new()
    {
        Stanytsia = amount.Stanytsia,
        Krai = amount.Krai,
        Kurin = amount.Kurin,
        Group = amount.Group,
        Total = amount.Total
    };

    /// <summary>A rate with its статті; a rate set before статті existed shows the three standard ones.</summary>
    public static KurinDuesRateDto ToDto(this KurinDuesRate rate) => new()
    {
        FromQuarter = DuesQuarter.FromIndex(rate.FromQuarter).ToDto(),
        TagKey = rate.TagKey,
        Articles = (rate.Articles.Count > 0
                ? rate.Articles
                : KurinDuesArticles.Standard(rate.StanytsiaFull, rate.StanytsiaReduced, rate.KurinShare))
            .Select(a => new DuesArticleDto
            {
                Name = a.Name,
                Recipient = a.Recipient,
                Amount = a.Amount,
                ReducedAmount = a.ReducedAmount
            })
            .ToList(),
        StanytsiaFull = rate.StanytsiaFull,
        StanytsiaReduced = rate.StanytsiaReduced,
        KurinShare = rate.KurinShare,
        KurinReduced = rate.KurinReduced ?? rate.KurinShare,
        KraiFull = rate.KraiFull,
        KraiReduced = rate.KraiReduced
    };

    public static KraiConcessionDto ToDto(this DuesConcessionRule rule) => new()
    {
        Key = rule.Key,
        Name = rule.Name,
        Kind = rule.Kind,
        Value = rule.Value,
        AppliesTo = PartNames(rule.Parts)
    };

    public static DuesConcessionGrantDto ToDto(this DuesConcessionGrant grant, DuesQuarter current) => new()
    {
        GrantKey = grant.DuesConcessionGrantKey,
        ConcessionKey = grant.ConcessionKey,
        Name = grant.Name,
        Kind = grant.Kind,
        Value = grant.Value,
        AppliesTo = PartNames(grant.Parts),
        FromQuarter = DuesQuarter.FromIndex(grant.FromQuarter).ToDto(),
        ToQuarter = grant.ToQuarter is { } end ? DuesQuarter.FromIndex(end).ToDto() : null,
        Reason = grant.Reason,
        IsActiveNow = grant.AppliesTo(current)
    };

    /// <summary>Every пластовий рік from the one <paramref name="first"/> falls in to today's, newest first.</summary>
    public static IReadOnlyList<PlastYearDto> Years(DuesQuarter first, DuesQuarter current)
    {
        var years = new List<PlastYearDto>();
        for (var year = PlastYear.Of(current); year >= PlastYear.Of(first); year--)
        {
            years.Add(new PlastYearDto
            {
                StartYear = year,
                Label = PlastYear.Label(year),
                Quarters = PlastYear.QuartersOf(year).Select(ToDto).ToList()
            });
        }

        return years;
    }

    private static IReadOnlyList<string> PartNames(DuesParts parts) =>
    [
        .. (parts & DuesParts.Stanytsia) != 0 ? [KraiDues.PartStanytsia] : Array.Empty<string>(),
        .. (parts & DuesParts.Krai) != 0 ? [KraiDues.PartKrai] : Array.Empty<string>(),
        .. (parts & DuesParts.Kurin) != 0 ? [KraiDues.PartKurin] : Array.Empty<string>(),
        .. (parts & DuesParts.Group) != 0 ? [KraiDues.PartGroup] : Array.Empty<string>()
    ];
}
