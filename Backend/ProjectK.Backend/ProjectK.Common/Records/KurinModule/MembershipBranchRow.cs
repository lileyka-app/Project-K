using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// An open membership of a person, with one гілка of the kurin it is in: a kurin that holds УСП and
/// УПС gives two rows. Read as stored; whether this makes the person a senior is
/// <c>SeniorStandingRules</c>' to say.
/// </summary>
public sealed record MembershipBranchRow(MembershipKind Kind, KurinBranch Branch);
