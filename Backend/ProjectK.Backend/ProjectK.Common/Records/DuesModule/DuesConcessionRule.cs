using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Records.DuesModule;

/// <summary>
/// How a пільга of the край changes a quarter's full вкладка: a percent off, a sum off, or nothing
/// to pay, on the parts it names. The same rule is kept on the край's profile and copied onto each
/// person it is given to.
/// </summary>
public sealed record DuesConcessionRule(string Key, string Name, string Kind, decimal Value, DuesParts Parts)
{
    /// <summary>
    /// The quarter's вкладка with the пільга. A sum off is shared between the parts it names in
    /// proportion to them, so no part goes below zero and the cents add up.
    /// </summary>
    public DuesAmount Apply(DuesAmount full)
    {
        var stanytsia = Covers(DuesParts.Stanytsia) ? full.Stanytsia : 0;
        var krai = Covers(DuesParts.Krai) ? full.Krai : 0;
        var kurin = Covers(DuesParts.Kurin) ? full.Kurin : 0;
        var group = Covers(DuesParts.Group) ? full.Group : 0;
        var covered = stanytsia + krai + kurin + group;
        if (covered <= 0)
        {
            return full;
        }

        var off = Kind switch
        {
            KraiDuesNames.Exempt => covered,
            KraiDuesNames.Amount => Math.Min(Value, covered),
            _ => Round(covered * Math.Clamp(Value, 0, 100) / 100)
        };

        // Shared out part by part; the last covered part takes what rounding left.
        var parts = new[] { stanytsia, krai, kurin, group };
        var cuts = new decimal[parts.Length];
        var last = Array.FindLastIndex(parts, part => part > 0);
        var given = 0m;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] <= 0)
            {
                continue;
            }

            cuts[i] = Math.Min(parts[i], i == last ? off - given : Round(off * parts[i] / covered));
            given += cuts[i];
        }

        return new DuesAmount(
            full.Stanytsia - cuts[0],
            full.Kurin - cuts[2],
            full.Group - cuts[3],
            full.Krai - cuts[1]);
    }

    private bool Covers(DuesParts part) => (Parts & part) == part;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>The parts named in a profile, as flags; a name this Lilejka does not know is left out.</summary>
    public static DuesParts PartsOf(IEnumerable<string>? names)
    {
        var parts = DuesParts.None;
        foreach (var name in names ?? [])
        {
            parts |= name?.Trim().ToLowerInvariant() switch
            {
                KraiDuesNames.PartStanytsia => DuesParts.Stanytsia,
                KraiDuesNames.PartKrai => DuesParts.Krai,
                KraiDuesNames.PartKurin => DuesParts.Kurin,
                KraiDuesNames.PartGroup => DuesParts.Group,
                _ => DuesParts.None
            };
        }

        return parts;
    }
}
