using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Rules.KurinModule;

/// <summary>
/// A закріплення за гуртком counts as an office: whoever runs a гурток of a kurin as виховник is a
/// Виховник of its КВ, whether or not the провід screen ever seated them. Access reads it that way,
/// the sidebar names the person that, and the КВ list has to show the same people — so the rule is
/// said once, here, and the reads hand over only what is stored.
/// <para>
/// In <c>Common</c> rather than the kurin module because the review notices of another module
/// list the КВ too. The roster's кадра asks <c>KurinRosterRules</c>, which counts the same
/// assignment.
/// </para>
/// </summary>
public static class MentorOfficeRules
{
    /// <summary>The office an assignment in force counts as.</summary>
    public static readonly MemberOffice Office = new(LeadershipType.KV, LeadershipRole.Vykhovnyk);

    /// <summary>
    /// The offices an account holds in a kurin: its seats, and <see cref="Office"/> once when it runs
    /// a гурток there and is not seated as that already.
    /// </summary>
    public static IReadOnlyList<MemberOffice> WithAssignment(IReadOnlyCollection<MemberOffice> seated, bool runsAGroup)
        => runsAGroup && !seated.Contains(Office) ? [.. seated, Office] : [.. seated];

    /// <summary>
    /// The КВ of a kurin as it is shown: everyone seated in it, then every виховник not seated, as
    /// <see cref="Office"/>. A person seated in the КВ keeps the office they are seated in.
    /// </summary>
    public static IReadOnlyList<MemberLookupRow> KvWithMentors(IReadOnlyList<MemberLookupRow> seated, IEnumerable<MemberLookupRow> mentors)
    {
        var listed = seated.Select(holder => holder.MemberKey).ToHashSet();
        return
        [
            .. seated,
            .. mentors.Where(mentor => listed.Add(mentor.MemberKey)).Select(mentor => mentor with { Office = Office })
        ];
    }
}
