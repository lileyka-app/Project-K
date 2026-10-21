using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// The kurin's part of a quarterly вкладка, from one quarter until the next change: the статті the
/// kurin set, in the order it set them.
/// <para>
/// The ledger reads the totals by where the money goes (<see cref="StanytsiaFull"/> and the rest).
/// They are kept beside the статті and set from them by <see cref="SetArticles"/>.
/// </para>
/// </summary>
public class KurinDuesRate : Entity
{
    public Guid KurinDuesRateKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    /// <summary>The first quarter it applies to, as <c>DuesQuarter.Index</c>.</summary>
    public int FromQuarter { get; set; }

    public List<KurinDuesArticle> Articles { get; set; } = [];

    public decimal StanytsiaFull { get; set; }
    public decimal StanytsiaReduced { get; set; }
    public decimal KurinShare { get; set; }

    /// <summary>The kurin part with a пільга. Null on rates set before статті: the пільга did not touch it.</summary>
    public decimal? KurinReduced { get; set; }

    /// <summary>What goes to the край on its own transfer. Zero until a kurin names a край стаття.</summary>
    public decimal KraiFull { get; set; }
    public decimal KraiReduced { get; set; }

    public Guid? SetByUserKey { get; set; }

    /// <summary>Replaces the статті and works the totals out from them.</summary>
    public void SetArticles(IEnumerable<KurinDuesArticle> articles)
    {
        Articles = articles.ToList();
        var up = Articles.Where(a => a.Recipient == DuesRecipient.Stanytsia).ToList();
        var own = Articles.Where(a => a.Recipient == DuesRecipient.Kurin).ToList();
        var krai = Articles.Where(a => a.Recipient == DuesRecipient.Krai).ToList();
        StanytsiaFull = up.Sum(a => a.Amount);
        StanytsiaReduced = up.Sum(a => a.ReducedAmount);
        KraiFull = krai.Sum(a => a.Amount);
        KraiReduced = krai.Sum(a => a.ReducedAmount);
        KurinShare = own.Sum(a => a.Amount);
        KurinReduced = own.Sum(a => a.ReducedAmount);
    }
}
