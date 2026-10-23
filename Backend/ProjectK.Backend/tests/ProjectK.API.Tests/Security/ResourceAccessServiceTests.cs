using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Security;

public class ResourceAccessServiceTests
{
    // System roles that reproduce the historic tiers: Зв'язковий = whole-kurin manager,
    // Гуртковий = group leader, Member = plain user.
    private static readonly string AdminRole = SystemRole.Admin;
    private static readonly string ManagerRole = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string MentorRole = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    private static readonly string MemberRole = SystemRole.Member;

    [Fact]
    public async Task UnauthenticatedUser_ShouldBeDenied()
    {
        var fixture = CreateFixture(false, Guid.NewGuid(), null, MemberRole);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, Guid.NewGuid());

        Assert.False(decision.IsAllowed);
        Assert.Contains("not authenticated", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_ShouldBypassChecks()
    {
        var fixture = CreateFixture(true, null, null, AdminRole);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Manage, Guid.NewGuid());

        Assert.True(decision.IsAllowed);
        Assert.Contains("Admin bypass", decision.Reason);
    }

    [Fact]
    public async Task ScopedAdmin_ShouldBeDeniedForDifferentKurinScope()
    {
        var scopedKurinKey = Guid.NewGuid();
        var otherKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, scopedKurinKey, null, AdminRole);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(otherKurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("different kurin", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScopedAdmin_ShouldBeAllowedForSameKurinScope()
    {
        var scopedKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, scopedKurinKey, null, AdminRole);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(scopedKurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Manage, memberKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Manager_ShouldBeAllowedForSameKurinScope()
    {
        var kurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, null, ManagerRole);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Delete, memberKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Manager_ShouldBeDeniedForDifferentKurinScope()
    {
        var userKurinKey = Guid.NewGuid();
        var resourceKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, userKurinKey, null, ManagerRole);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(resourceKurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("different kurin", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_ShouldBeDeniedForIrreversibleKurinActions()
    {
        var fixture = CreateFixture(true, Guid.NewGuid(), null, ManagerRole);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Kurin, ResourceAction.Delete, Guid.NewGuid());

        Assert.False(decision.IsAllowed);
        Assert.Contains("No permission", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_ShouldBeAllowedToUpdateOwnKurin()
    {
        var kurinKey = Guid.NewGuid();
        var fixture = CreateFixture(true, kurinKey, null, ManagerRole);
        fixture.Scope(ResourceType.Kurin, kurinKey, new ResourceScope(kurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Kurin, ResourceAction.Update, kurinKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Manager_ShouldBeAllowedToReadGroupInOwnKurin()
    {
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var fixture = CreateFixture(true, kurinKey, null, ManagerRole);
        fixture.Scope(ResourceType.Group, groupKey, new ResourceScope(kurinKey, groupKey, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Group, ResourceAction.Read, groupKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Mentor_ShouldBeDeniedForGroupDeleteAction()
    {
        var fixture = CreateFixture(true, Guid.NewGuid(), Guid.NewGuid(), MentorRole);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Group, ResourceAction.Delete, Guid.NewGuid());

        Assert.False(decision.IsAllowed);
        Assert.Contains("No permission", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_ShouldBeDeniedForNonReadAction()
    {
        var fixture = CreateFixture(true, Guid.NewGuid(), Guid.NewGuid(), MemberRole);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Group, ResourceAction.Create, Guid.NewGuid());

        Assert.False(decision.IsAllowed);
        Assert.Contains("No permission", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_ShouldBeAllowedToUpdateOwnMemberProfile()
    {
        var kurinKey = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, null, new[] { MemberRole }, userId);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, null, userId));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task User_ShouldBeDeniedToUpdateAnotherMemberProfile()
    {
        var kurinKey = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, null, new[] { MemberRole }, userId);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, null, Guid.NewGuid()));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("own resources", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mentor_ShouldBeAllowedToUpdateMemberInOwnGroup()
    {
        var kurinKey = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var mentorGroupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, mentorGroupKey, new[] { MentorRole }, mentorUserId);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, mentorGroupKey, null));


        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Mentor_ShouldBeDeniedToUpdateMemberInAnotherGroup()
    {
        var kurinKey = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var mentorGroupKey = Guid.NewGuid();
        var foreignGroupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, mentorGroupKey, new[] { MentorRole }, mentorUserId);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, foreignGroupKey, null));


        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("led groups", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mentor_ShouldBeAllowedToUpdateMemberInAssignedSecondaryGroup_WhenAssignmentModelIsImplemented()
    {
        var kurinKey = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var mentorPrimaryGroupKey = Guid.NewGuid();
        var assignedSecondaryGroupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, mentorPrimaryGroupKey, new[] { MentorRole }, mentorUserId);

        fixture.MentorGroups(assignedSecondaryGroupKey);

        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(kurinKey, assignedSecondaryGroupKey, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task UserReadLeadership_ShouldResolveScopeViaGroup()
    {
        var kurinKey = Guid.NewGuid();
        var leadershipKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, null, MemberRole);
        fixture.Scope(ResourceType.Leadership, leadershipKey, new ResourceScope(kurinKey, groupKey, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Leadership, ResourceAction.Read, leadershipKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task MissingUserKurinScopeClaim_ShouldBeDenied()
    {
        var memberKey = Guid.NewGuid();
        var fixture = CreateFixture(true, null, null, ManagerRole);
        fixture.Scope(ResourceType.Member, memberKey, new ResourceScope(Guid.NewGuid(), null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("scope claim", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResourceNotFound_ShouldBeDenied()
    {
        var memberKey = Guid.NewGuid();
        var fixture = CreateFixture(true, Guid.NewGuid(), null, ManagerRole);
        fixture.Scope(ResourceType.Member, memberKey, null);

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, memberKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("not found", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeProgressScopeResolution_ShouldAllow_WhenMemberInSameKurin()
    {
        var kurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var probeProgressKey = Guid.NewGuid();

        var fixture = CreateFixture(true, kurinKey, null, ManagerRole);

        fixture.Scope(ResourceType.ProbeProgress, probeProgressKey, new ResourceScope(kurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.ProbeProgress, ResourceAction.Read, probeProgressKey);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task BadgeProgressScopeResolution_ShouldDeny_WhenMemberInForeignKurin()
    {
        var userKurinKey = Guid.NewGuid();
        var foreignKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var badgeProgressKey = Guid.NewGuid();

        var fixture = CreateFixture(true, userKurinKey, null, ManagerRole);

        fixture.Scope(ResourceType.BadgeProgress, badgeProgressKey, new ResourceScope(foreignKurinKey, null, null));

        var decision = await fixture.Service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Read, badgeProgressKey);

        Assert.False(decision.IsAllowed);
        Assert.Contains("different kurin", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MentorScope_ShouldBeResolvedOnce_AcrossRepeatedWriteChecks()
    {
        var kurinKey = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var (service, scopeReader, _) = CreateCachingFixture(kurinKey, groupKey, mentorUserId);
        scopeReader
            .Setup(x => x.GetScopeAsync(ResourceType.Member, memberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceScope(kurinKey, groupKey, null));

        await service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);
        await service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        scopeReader.Verify(
            x => x.GetLedGroupKeysAsync(mentorUserId, kurinKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MentorScope_ShouldBeReResolved_AfterInvalidation()
    {
        var kurinKey = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var (service, scopeReader, cache) = CreateCachingFixture(kurinKey, groupKey, mentorUserId);
        scopeReader
            .Setup(x => x.GetScopeAsync(ResourceType.Member, memberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceScope(kurinKey, groupKey, null));

        await service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);
        cache.Invalidate(BackendCachePolicies.MentorScopeReads);
        await service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey);

        scopeReader.Verify(
            x => x.GetLedGroupKeysAsync(mentorUserId, kurinKey, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static (ResourceAccessService Service, Mock<IResourceScopeReader> ScopeReader, IBackendCache Cache) CreateCachingFixture(
        Guid kurinKey,
        Guid groupKey,
        Guid mentorUserId)
    {
        var roleValues = new[] { MentorRole };

        var currentUserContext = new Mock<ICurrentUserContext>();
        currentUserContext.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserContext.SetupGet(x => x.KurinKey).Returns(kurinKey);
        currentUserContext.SetupGet(x => x.UserId).Returns(mentorUserId);
        currentUserContext.SetupGet(x => x.Roles).Returns(roleValues);
        currentUserContext
            .Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns((string role) => roleValues.Contains(role, StringComparer.OrdinalIgnoreCase));

        var scopeReader = new Mock<IResourceScopeReader>();
        scopeReader
            .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { groupKey });

        var cache = new MemoryBackendCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryBackendCache>.Instance);
        var service = new ResourceAccessService(scopeReader.Object, currentUserContext.Object, cache, new CurrentPermissions(currentUserContext.Object, new FixedKurinAccessPolicyProvider()));
        return (service, scopeReader, cache);
    }

    private static ResourceAccessFixture CreateFixture(
        bool isAuthenticated,
        Guid? kurinKey,
        Guid? groupKey,
        params string[] roles)
    {
        return CreateFixture(isAuthenticated, kurinKey, groupKey, roles, Guid.NewGuid());
    }

    private static ResourceAccessFixture CreateFixture(
        bool isAuthenticated,
        Guid? kurinKey,
        Guid? groupKey,
        string[] roles,
        Guid userId)
    {
        var roleValues = roles;

        var currentUserContext = new Mock<ICurrentUserContext>();
        currentUserContext.SetupGet(x => x.IsAuthenticated).Returns(isAuthenticated);
        currentUserContext.SetupGet(x => x.KurinKey).Returns(kurinKey);
        currentUserContext.SetupGet(x => x.UserId).Returns(userId);
        currentUserContext.SetupGet(x => x.Roles).Returns(roleValues);
        currentUserContext
            .Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns((string role) => roleValues.Contains(role, StringComparer.OrdinalIgnoreCase));

        var scopeReader = new Mock<IResourceScopeReader>();
        scopeReader
            .Setup(x => x.GetScopeAsync(It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceScope?)null);

        // A mentor covers the group they belong to unless a test says otherwise.
        scopeReader
            .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(groupKey.HasValue ? new[] { groupKey.Value } : Array.Empty<Guid>());

        var service = new ResourceAccessService(scopeReader.Object, currentUserContext.Object, new PassThroughBackendCache(), new CurrentPermissions(currentUserContext.Object, new FixedKurinAccessPolicyProvider()));
        return new ResourceAccessFixture(service, scopeReader);
    }

    // Runs the factory every time — keeps these authorization tests independent of the
    // real cache. Cache hit/invalidation behaviour is covered by MentorScope_* tests.
    private sealed class PassThroughBackendCache : IBackendCache
    {
        public Task<T> GetOrCreateAsync<T>(CachePolicy policy, string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken, CacheScopeContext? scopeContext = null)
            => factory(cancellationToken);

        public void Invalidate(CachePolicy policy) { }

        public void InvalidateByPrefix(string prefix) { }
    }

    private sealed record ResourceAccessFixture(
        ResourceAccessService Service,
        Mock<IResourceScopeReader> ScopeReader)
    {
        public void Scope(ResourceType resourceType, Guid resourceKey, ResourceScope? scope)
        {
            ScopeReader
                .Setup(x => x.GetScopeAsync(resourceType, resourceKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scope);
        }

        public void MentorGroups(params Guid[] groupKeys)
        {
            ScopeReader
                .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(groupKeys);
        }
    }
}
