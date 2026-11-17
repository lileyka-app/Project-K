using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Tests.AuthModule;

/// <summary>
/// The two decisions the scope repository used to make in SQL, now made over the facts it reads. Each
/// case is one the repository answered before, with the answer it gave.
/// </summary>
public class ResourceScopeReaderTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static MembershipBranchRow Row(MembershipKind kind, KurinBranch branch) => new(kind, branch);

    [Fact]
    public void ASeniorServingAsStaffInAYouthKurin_IsASenior() =>
        SeniorStandingRules.StandsInSeniorKurin(
                [Row(MembershipKind.Staff, KurinBranch.UPYu), Row(MembershipKind.Youth, KurinBranch.USP)])
            .Should().BeTrue();

    [Fact]
    public void StaffInAYouthKurinAlone_IsNoSenior() =>
        SeniorStandingRules.StandsInSeniorKurin([Row(MembershipKind.Staff, KurinBranch.UPYu)]).Should().BeFalse();

    [Fact]
    public void AYouthInASeniorKurin_IsASenior() =>
        SeniorStandingRules.StandsInSeniorKurin(
                [Row(MembershipKind.Youth, KurinBranch.USP), Row(MembershipKind.Youth, KurinBranch.UPS)])
            .Should().BeTrue();

    [Fact]
    public void AYouthStillInAnUpyuKurin_IsNoSenior_BesideASeniorKurin() =>
        SeniorStandingRules.StandsInSeniorKurin(
                [Row(MembershipKind.Youth, KurinBranch.UPYu), Row(MembershipKind.Staff, KurinBranch.UPS)])
            .Should().BeFalse();

    [Fact]
    public void NoOpenMembership_IsNoSenior() =>
        SeniorStandingRules.StandsInSeniorKurin([]).Should().BeFalse();

    [Fact]
    public void AMentorWithoutAnOffice_LeadsTheAssignedGroup() =>
        LedGroupRules.LedGroupKeys(new LedGroupSourcesRow([], [A], null)).Should().Equal(A);

    [Fact]
    public void TheOwnGroup_IsLed_WithNothingElse() =>
        LedGroupRules.LedGroupKeys(new LedGroupSourcesRow([], [], C)).Should().Equal(C);

    [Fact]
    public void LedGroups_AreOfficesThenAssignmentsThenTheOwnGroup_EachOnce() =>
        LedGroupRules.LedGroupKeys(new LedGroupSourcesRow([A, B], [B, C], A)).Should().Equal(A, B, C);

    [Fact]
    public void NothingHeld_LeadsNothing() =>
        LedGroupRules.LedGroupKeys(new LedGroupSourcesRow([], [], null)).Should().BeEmpty();

    [Fact]
    public async Task TheReader_AppliesTheRules_ToWhatTheRepositoryReads()
    {
        var user = Guid.NewGuid();
        var kurin = Guid.NewGuid();
        var person = Guid.NewGuid();
        var repository = new Mock<IResourceScopeRepository>();
        repository.Setup(r => r.GetLedGroupSourcesAsync(user, kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedGroupSourcesRow([A], [B], A));
        repository.Setup(r => r.GetOpenMembershipBranchesAsync(ResourceType.BadgeProgress, person, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Row(MembershipKind.Staff, KurinBranch.UPYu), Row(MembershipKind.Youth, KurinBranch.UPS)]);
        var reader = ScopeFacts.Reader(repository.Object);

        (await reader.GetLedGroupKeysAsync(user, kurin)).Should().Equal(A, B);
        (await reader.StandsInSeniorKurinAsync(ResourceType.BadgeProgress, person)).Should().BeTrue();
    }
}
