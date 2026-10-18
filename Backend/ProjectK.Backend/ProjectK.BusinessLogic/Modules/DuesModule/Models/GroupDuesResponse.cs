using ProjectK.Common.Models.KraiProfiles;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Models;

/// <summary>A гурток's whole box in one read: who owes what, what happened, what is in the box.</summary>
public sealed class GroupDuesResponse
{
    public Guid GroupKey { get; init; }
    public Guid KurinKey { get; init; }
    public string GroupName { get; init; } = string.Empty;

    /// <summary>ISO 4217 code of what the amounts are counted in: the край's currency.</summary>
    public string Currency { get; init; } = "UAH";
    /// <summary>How the край writes numbers, such as uk-UA.</summary>
    public string NumberLocale { get; init; } = "uk-UA";
    /// <summary>Where the край's kurins hand the вкладка up: stanytsia, krai or both.</summary>
    public string Upstream { get; init; } = KraiDues.Stanytsia;

    public QuarterDto CurrentQuarter { get; init; } = new();

    /// <summary>The пластові роки the table can show, newest first, each with its four quarters.</summary>
    public IReadOnlyList<PlastYearDto> Years { get; init; } = [];

    public IReadOnlyList<KurinDuesRateDto> KurinRates { get; init; } = [];
    public IReadOnlyList<GroupDuesRateDto> GroupRates { get; init; } = [];

    public IReadOnlyList<DuesAccountDto> Accounts { get; init; } = [];
    public IReadOnlyList<DuesEntryDto> Entries { get; init; } = [];

    public DuesBoxDto Box { get; init; } = new();
    public DuesHandoverDto Handover { get; init; } = new();

    /// <summary>Everyone in the kurin, for "who collected" and the like.</summary>
    public IReadOnlyList<DuesPersonDto> People { get; init; } = [];

    public DuesViewerDto Viewer { get; init; } = new();
}

public sealed class QuarterDto
{
    public int Year { get; init; }
    public int Number { get; init; }
}

public sealed class PlastYearDto
{
    public int StartYear { get; init; }
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<QuarterDto> Quarters { get; init; } = [];
}

public sealed class KurinDuesRateDto
{
    public QuarterDto FromQuarter { get; init; } = new();

    /// <summary>The kurin's статті, in the order it set them. The amounts below are their totals.</summary>
    public IReadOnlyList<DuesArticleDto> Articles { get; init; } = [];

    public decimal StanytsiaFull { get; init; }
    public decimal StanytsiaReduced { get; init; }
    public decimal KurinShare { get; init; }
    public decimal KurinReduced { get; init; }
    public decimal KraiFull { get; init; }
    public decimal KraiReduced { get; init; }

    public static KurinDuesRateDto From(KurinDuesRate rate, QuarterDto fromQuarter) => new()
    {
        FromQuarter = fromQuarter,
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
}

public sealed class DuesArticleDto
{
    public string Name { get; init; } = string.Empty;
    public DuesRecipient Recipient { get; init; }
    public decimal Amount { get; init; }
    public decimal ReducedAmount { get; init; }
}

public sealed class GroupDuesRateDto
{
    public QuarterDto FromQuarter { get; init; } = new();
    public decimal GroupShare { get; init; }
}

public sealed class DuesAmountDto
{
    public decimal Stanytsia { get; init; }
    public decimal Krai { get; init; }
    public decimal Kurin { get; init; }
    public decimal Group { get; init; }
    public decimal Total { get; init; }
}

public sealed class DuesAccountQuarterDto
{
    public QuarterDto Quarter { get; init; } = new();
    public DuesAmountDto Charged { get; init; } = new();
    public DuesAmountDto Paid { get; init; } = new();
    public decimal Balance { get; init; }
    public bool IsConcession { get; init; }
}

/// <summary>Where the person stands towards this гурток's box.</summary>
public enum DuesAccountStanding
{
    /// <summary>A youth of this гурток today.</summary>
    Current,

    /// <summary>Moved to another гурток of the kurin, with quarters still owed here.</summary>
    Moved,

    /// <summary>Left the kurin, with quarters still owed here.</summary>
    Left
}

public static class DuesAccountStandings
{
    /// <summary>Gone from the kurin first; otherwise here or moved on, by the гурток they stand in now.</summary>
    public static DuesAccountStanding Of(bool hasLeft, bool standsInThisGroup)
    {
        if (hasLeft)
        {
            return DuesAccountStanding.Left;
        }

        return standsInThisGroup ? DuesAccountStanding.Current : DuesAccountStanding.Moved;
    }
}

public sealed class DuesAccountDto
{
    public Guid MembershipKey { get; init; }
    public Guid MemberKey { get; init; }
    public string FullName { get; init; } = string.Empty;
    public DuesAccountStanding Standing { get; init; }
    public bool IsConcessionNow { get; init; }
    public IReadOnlyList<DuesAccountQuarterDto> Quarters { get; init; } = [];
    public decimal Charged { get; init; }
    public decimal Payments { get; init; }
    public decimal Balance { get; init; }
}

public sealed class DuesEntryDto
{
    public Guid DuesEntryKey { get; init; }
    public DuesEntryKind Kind { get; init; }
    public DuesPaymentMethod Method { get; init; }
    public DuesPaymentMethod? CounterMethod { get; init; }
    public decimal Amount { get; init; }
    public DateOnly OccurredOn { get; init; }
    public Guid? MembershipKey { get; init; }
    public string? MemberName { get; init; }
    public Guid? CollectedByMemberKey { get; init; }
    public string? CollectedByName { get; init; }
    public string? Note { get; init; }
    public bool IsVerified { get; init; }
    public DateTime? VerifiedAtUtc { get; init; }
    public string? VerifiedByName { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class DuesBoxDto
{
    public decimal Cash { get; init; }
    public decimal Card { get; init; }
    public decimal Total { get; init; }
    public decimal ToForward { get; init; }
    public decimal Own { get; init; }
    public decimal InTransit { get; init; }
}

public sealed class DuesHandoverDto
{
    public decimal OwedUp { get; init; }
    public decimal Transferred { get; init; }
    public decimal Received { get; init; }
    public decimal Outstanding { get; init; }
}

public sealed class DuesPersonDto
{
    public Guid MemberKey { get; init; }
    public string FullName { get; init; } = string.Empty;
}

/// <summary>What the person reading may do here, so the screen offers only that.</summary>
public sealed class DuesViewerDto
{
    public bool CanKeep { get; init; }
    public bool CanVerify { get; init; }

    /// <summary>Whether they may set the станиця and kurin parts — the Звʼязковий and the курінний скарбник.</summary>
    public bool CanSetKurinRates { get; init; }
}
