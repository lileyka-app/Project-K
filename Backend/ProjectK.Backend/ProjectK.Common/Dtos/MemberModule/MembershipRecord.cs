using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>
/// One stretch of belonging, as the kurin module reports it. Carries the kurin's own details so the
/// reader does not have to go and look them up — this crosses a module boundary, and one day a
/// network with it.
/// </summary>
public sealed record MembershipRecord(
    Guid MembershipKey,
    Guid KurinKey,
    int? KurinNumber,
    KurinBranch Branch,
    string? KurinNamedAfter,
    Guid? GroupKey,
    string? GroupName,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc,
    IReadOnlyList<KurinNumbering>? KurinBranches = null)
{
    public bool IsCurrent => LeftAtUtc is null;

    /// <summary>Every гілка of the kurin with its number there; the main one alone when only that was read.</summary>
    public IReadOnlyList<KurinNumbering> Numbering => KurinBranches ?? [new KurinNumbering(Branch, KurinNumber)];

    /// <summary>
    /// The same stretch in the person's own line. A kurin in both senior гілки holds a сеніор in its
    /// УПС and a старший пластун in its УСП, whichever гілка is the kurin's main one; the ступінь
    /// decides, not the kurin. Unchanged when the kurin has no гілка of that line.
    /// </summary>
    public MembershipRecord InLineOf(IEnumerable<PlastLevel> levels)
    {
        var line = PlastLadderRules.Highest(levels) is { } top ? PlastLadderRules.BranchOf(top) : KurinBranch.UPYu;
        if (line == Branch || !KurinBranchSetRules.IsSenior(line) || !KurinBranchSetRules.IsSenior(Branch))
        {
            return this;
        }

        return Numbering.FirstOrDefault(n => n.Branch == line) is { } numbering
            ? this with { Branch = numbering.Branch, KurinNumber = numbering.Number }
            : this;
    }
}
