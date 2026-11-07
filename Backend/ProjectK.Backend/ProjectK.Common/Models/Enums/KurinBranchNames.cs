using System.Collections.Generic;
using System.Linq;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Enums;

/// <summary>
/// How a гілка and a senior kurin's numbers read in a letter or a notification. The web mirrors it in
/// <c>kurin-label.function.ts</c>.
/// </summary>
public static class KurinBranchNames
{
    public static string Label(KurinBranch branch) => branch switch
    {
        KurinBranch.USP => "УСП",
        KurinBranch.UPS => "УПС",
        _ => "УПЮ"
    };

    /// <summary>«5-й курінь УСП · 20-й курінь УПС».</summary>
    public static string DescribeSenior(IEnumerable<KurinNumbering> branches)
        => string.Join(" · ", KurinBranchSet.Ordered(branches, b => b.Branch)
            .Select(b => b.Number is int number ? $"{number}-й курінь {Label(b.Branch)}" : $"курінь {Label(b.Branch)}"));

    /// <summary>
    /// A kurin in a line of text, as the web's <c>kurinBranchesName</c> lower-cased: «к. ч. 47»,
    /// «підг. курінь», or a senior kurin by its гілки, «5-й курінь УСП».
    /// </summary>
    public static string InText(ProjectK.Common.Entities.KurinModule.Kurin kurin) => InText(NumberingOf(kurin));

    /// <summary>The kurin's гілки, or its main one alone when the rows were not read.</summary>
    public static IReadOnlyList<KurinNumbering> NumberingOf(ProjectK.Common.Entities.KurinModule.Kurin kurin)
        => kurin.Numbering is { Count: > 0 } numbering ? numbering : [new KurinNumbering(kurin.Branch, kurin.Number)];

    /// <inheritdoc cref="InText(ProjectK.Common.Entities.KurinModule.Kurin)"/>
    public static string InText(IReadOnlyCollection<KurinNumbering> branches)
    {
        var senior = branches.Where(b => KurinBranchSet.IsSenior(b.Branch)).ToList();
        if (senior.Count > 0)
        {
            return DescribeSenior(senior);
        }

        return branches.FirstOrDefault()?.Number is int number ? $"к. ч. {number}" : "підг. курінь";
    }
}
