using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.BusinessLogic.Tests.AuthModule;

/// <summary>
/// The parts <see cref="ResourceAccessService"/> decides with: whether a granted scope reaches a
/// resource, who signs off whose work, and the guard on a senior's youth record.
/// </summary>
public sealed class ResourceAccessPartsTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Guid _resourceKey = Guid.NewGuid();
    private readonly Mock<IResourceScopeRepository> _reader = new();
    private readonly Mock<ICurrentUserContext> _user = new();

    public ResourceAccessPartsTests()
    {
        _user.SetupGet(u => u.KurinKey).Returns(_kurinKey);
        _user.SetupGet(u => u.UserId).Returns(_userKey);
        _reader.Setup(r => r.GetLedGroupKeysAsync(_userKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([_groupKey]);
    }

    private ResourceScopeService Scopes() => new(_reader.Object, _user.Object, new PassThroughBackendCache());

    private void ResourceIn(Guid kurinKey, Guid? groupKey, Guid? ownerUserKey) =>
        _reader.Setup(r => r.GetScopeAsync(ResourceType.Member, _resourceKey, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceScope(kurinKey, groupKey, ownerUserKey));

    [Fact]
    public async Task Check_ReachesALedGurtok_ButNotAnotherOne()
    {
        ResourceIn(_kurinKey, _groupKey, Guid.NewGuid());
        (await Scopes().CheckAsync(AccessScope.OwnGroups, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeTrue();

        ResourceIn(_kurinKey, Guid.NewGuid(), Guid.NewGuid());
        (await Scopes().CheckAsync(AccessScope.OwnGroups, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task Check_HoldsAKurinWideGrantToTheKurinTheUserActsIn()
    {
        ResourceIn(_kurinKey, null, null);
        (await Scopes().CheckAsync(AccessScope.KurinWide, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeTrue();

        ResourceIn(Guid.NewGuid(), null, null);
        (await Scopes().CheckAsync(AccessScope.KurinWide, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task Check_AnOwnGrantReachesOnlyTheUsersOwnRecord()
    {
        ResourceIn(_kurinKey, null, _userKey);
        (await Scopes().CheckAsync(AccessScope.Own, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeTrue();

        ResourceIn(_kurinKey, null, Guid.NewGuid());
        (await Scopes().CheckAsync(AccessScope.Own, ResourceType.Member, _resourceKey, default)).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void SignOff_CoversSigningAProbeAndReviewingABadge_NotReadingThem()
    {
        SignOffRules.Capabilities.Should().Contain([KurinCapability.SignProbe, KurinCapability.UnsignProbe, KurinCapability.ReviewBadge]);
        SignOffRules.SignsOff(ResourceType.ProbeProgress, ResourceAction.Read).Should().BeFalse();
    }

    [Fact]
    public async Task SeniorGuard_LetsANonSeniorThrough_AndKeepsASeniorsProbesAnArchive()
    {
        var guard = new SeniorYouthRecordService(_reader.Object, Scopes());
        _reader.Setup(r => r.StandsInSeniorKurinAsync(ResourceType.Member, _resourceKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        (await guard.RefusalAsync(ResourceType.ProbeProgress, AccessScope.KurinWide, ResourceType.Member, _resourceKey, ownerMayChange: false, default))
            .Should().BeNull();

        _reader.Setup(r => r.StandsInSeniorKurinAsync(ResourceType.Member, _resourceKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await guard.RefusalAsync(ResourceType.ProbeProgress, AccessScope.KurinWide, ResourceType.Member, _resourceKey, ownerMayChange: false, default))!
            .IsAllowed.Should().BeFalse();
        (await guard.RefusalAsync(ResourceType.BadgeProgress, AccessScope.KurinWide, ResourceType.Member, _resourceKey, ownerMayChange: false, default))
            .Should().BeNull();
        (await guard.RefusalAsync(ResourceType.BadgeProgress, AccessScope.OwnGroups, ResourceType.Member, _resourceKey, ownerMayChange: false, default))!
            .IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task SeniorGuard_LetsTheOwnerChangeWhatTheyMay_AndNothingElse()
    {
        var guard = new SeniorYouthRecordService(_reader.Object, Scopes());
        _reader.Setup(r => r.StandsInSeniorKurinAsync(ResourceType.Member, _resourceKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reader.Setup(r => r.GetPersonAccountKeyAsync(ResourceType.Member, _resourceKey, It.IsAny<CancellationToken>())).ReturnsAsync(_userKey);

        (await guard.RefusalAsync(ResourceType.Member, AccessScope.Own, ResourceType.Member, _resourceKey, ownerMayChange: true, default))
            .Should().BeNull();
        (await guard.RefusalAsync(ResourceType.BadgeProgress, AccessScope.KurinWide, ResourceType.Member, _resourceKey, ownerMayChange: false, default))!
            .IsAllowed.Should().BeFalse();
        SeniorYouthRecordService.ChangesYouthRecord(ResourceType.BadgeProgress, ResourceAction.Read).Should().BeFalse();
        SeniorYouthRecordService.ChangesYouthRecord(ResourceType.ProbeProgress, ResourceAction.Update).Should().BeTrue();
    }
}
