using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// Everything the report is assembled from, read in one pass. The people, their memberships and the
/// впорядники come as values, not as the member module's entities. What people have earned is not
/// here: that belongs to the probes and вмілості module and is asked of it through
/// <c>IMemberProgressDirectory</c>.
/// </summary>
public sealed record KurinReportSourceData(
    Kurin Kurin,
    IReadOnlyList<Group> Groups,
    IReadOnlyList<KurinReportSourceMentor> MentorAssignments,
    IReadOnlyList<KurinReportSourceMember> Members,
    IReadOnlyDictionary<Guid, AppUser> UsersByKey,
    IReadOnlyDictionary<Guid, KurinMembershipRecord> MembershipByMemberKey);
