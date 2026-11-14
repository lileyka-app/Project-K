using FluentAssertions;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The boundary v0.20.0 is building. Each rule is asserted against a baseline of the types already
/// known to cross the line: a new crossing fails immediately. The release is finished when every
/// list is empty, and an empty baseline is as strict as it looks — nothing may cross at all.
/// <para>
/// The rules read IL, not signatures — a handler that only ever touches a member through
/// <c>_unitOfWork.Members</c> inside a method body is caught just the same, and every entry below
/// is exactly that case.
/// </para>
/// <para>
/// Asserted as a subset rather than an exact match, and that is not laziness. Reading IL means the
/// answer depends on what the compiler emitted: <c>KurinReportDataService</c> crosses the line in a
/// Debug build and does not in a Release one, where the reference is optimised away. An
/// exact-match baseline therefore passed locally and failed in CI, which builds Release — these
/// tests were never green there. What the boundary actually needs is "nothing new", and that holds
/// in both. A baseline entry that has genuinely been fixed is removed by hand.
/// </para>
/// </summary>
public class MemberBoundaryRules
{
    /// <summary>
    /// Everyone outside the member's own module who still reads member data directly. Emptied by
    /// MM-03: every one of them now asks <c>IMemberDirectory</c>. It stays empty — MM-06 removes the
    /// last way back in by taking <c>Members</c> off <c>IUnitOfWork</c>.
    /// </summary>
    private static readonly string[] ReachesMemberDataDirectly = [];

    /// <summary>
    /// Empty, and the release's point: who may act on whom is decided from membership and offices.
    /// The member record is not read anywhere on the way to that decision, and this list going
    /// non-empty means it is again.
    /// </summary>
    private static readonly string[] AuthorizationStillKnowsAboutMember = [];

    /// <summary>
    /// Empty since MM-13a (DEBT-03): the kurin's report was the last reader of probe and вмілість
    /// rows outside their module, and now asks <c>IMemberProgressDirectory</c> for the whole roster
    /// in one call. Nothing may cross again.
    /// </summary>
    private static readonly string[] ReadsProgressDataDirectly = [];

    [Fact]
    public void MemberData_ShouldBeReachedOnlyFromItsOwnModule()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.(BusinessLogic|API|Common\.Entities)\..*")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.BusinessLogic\.Modules\.MemberModule(\..*)?")
            // Another module's entity may hold a MemberKey, never a Member navigation: that would be
            // a foreign key across the boundary, and the join it promises disappears with the module.
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.Common\.Entities\.MemberModule(\..*)?")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.API\.Controllers\.MemberModule(\..*)?")
            .Should().NotDependOnAny(typeof(Member), typeof(IMemberRepository));

        ProjectKArchitecture.Violations(rule).Should().BeSubsetOf(
            ReachesMemberDataDirectly,
            "the member module's boundary may shrink but never grow — update the baseline in the same commit");
    }

    [Fact]
    public void Authorization_ShouldNotKnowAboutMember()
    {
        var rule = Types()
            .That().ImplementInterface(typeof(IResourceAccessService))
            .Or().ImplementInterface(typeof(IResourceScopeRepository))
            .Or().ImplementInterface(typeof(ILoginResponseService))
            .Should().NotDependOnAny(typeof(Member), typeof(IMemberRepository));

        ProjectKArchitecture.Violations(rule).Should().BeSubsetOf(
            AuthorizationStillKnowsAboutMember,
            "who may act is decided from Membership and offices, never from the person's own record");
    }

    /// <summary>
    /// The other side of the same boundary. A person's dossier shows what they have taken and
    /// earned, and it gets it by asking the probes and вмілості module rather than by reading its
    /// tables — which is the only version of that read that still works once the module is elsewhere.
    /// </summary>
    [Fact]
    public void ProgressData_ShouldBeReachedOnlyFromItsOwnModule()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.(BusinessLogic|API|Common\.Entities)\..*")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.BusinessLogic\.Modules\.ProbesAndBadgesModule(\..*)?")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.Common\.Entities\.ProbesAndBadgesModule(\..*)?")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.API\.Controllers\.ProbesAndBadgesModule(\..*)?")
            .Should().NotDependOnAny(
                typeof(ProbeProgress),
                typeof(BadgeProgress),
                typeof(IProbeProgressRepository),
                typeof(IBadgeProgressRepository));

        ProjectKArchitecture.Violations(rule).Should().BeSubsetOf(
            ReadsProgressDataDirectly,
            "what a person has earned is asked of the module that holds it, through IMemberProgressDirectory");
    }

    [Fact]
    public void Modules_ShouldAnnounceWhatHappened_RatherThanWriteToTheInbox()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.BusinessLogic\.Modules\.(KurinModule|MemberModule|ProbesAndBadgesModule)(\..*)?")
            .Should().NotDependOnAny(typeof(INotificationService));

        ProjectKArchitecture.Violations(rule).Should().BeEmpty(
            "a module states that something happened and lets whoever cares react; "
            + "writing the inbox entry itself is what made the wording travel with the domain");
    }
}
