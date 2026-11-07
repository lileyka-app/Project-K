using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// One гілка a kurin belongs to and its number there. A УПЮ kurin has one; a senior kurin may have
/// two — Орден Хрестоносців is the 5th kurin of УСП and the 20th of УПС, one kurin with one roster.
/// Numbering is separate in each гілка of a край: a УПЮ kurin 5 and a УСП kurin 5 live side by side.
/// </summary>
public class KurinBranchNumber
{
    public Guid KurinKey { get; set; }
    public KurinBranch Branch { get; set; }

    /// <summary>Null for a підготовчий курінь, which has not been given a number yet.</summary>
    public int? Number { get; set; }

    /// <summary>
    /// The kurin's край, copied here so the database can hold a number unique per край and гілка.
    /// Written by the kurin itself (<c>Kurin.KraiCode</c>, <c>Kurin.SetBranches</c>), never on its own.
    /// </summary>
    public string KraiCode { get; set; } = string.Empty;
}
