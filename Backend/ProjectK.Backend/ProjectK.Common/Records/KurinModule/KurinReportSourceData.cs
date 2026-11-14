using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// Everything of the kurin's own the report is assembled from, read in one pass. What people have
/// earned is not here: that belongs to the probes and вмілості module and is asked of it through
/// <c>IMemberProgressDirectory</c>.
/// </summary>
public sealed record KurinReportSourceData(
    Kurin Kurin,
    IReadOnlyList<Group> Groups,
    IReadOnlyList<MentorAssignment> MentorAssignments,
    IReadOnlyList<Member> Members,
    IReadOnlyDictionary<Guid, AppUser> UsersByKey,
    IReadOnlyDictionary<Guid, Membership> MembershipByMemberKey);
