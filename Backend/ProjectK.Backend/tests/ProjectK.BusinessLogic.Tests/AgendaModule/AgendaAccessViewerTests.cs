using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

/// <summary>
/// Who the viewer is in the kurin an agenda page is about. Someone who stands in two kurins is a
/// member of both: in each, they are placed by their membership there, not by the kurin they joined last.
/// </summary>
public class AgendaAccessViewerTests
{
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _olderKurin = Guid.NewGuid();
    private readonly Guid _olderGroup = Guid.NewGuid();
    private readonly Guid _newerKurin = Guid.NewGuid();
    private readonly Guid _newerGroup = Guid.NewGuid();
    private readonly Mock<IMembershipDirectory> _memberships = new();

    public AgendaAccessViewerTests()
    {
        _memberships.Setup(x => x.FindInKurinByAccountAsync(_userKey, _olderKurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Placed(_olderKurin, _olderGroup));
        _memberships.Setup(x => x.FindInKurinByAccountAsync(_userKey, _newerKurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Placed(_newerKurin, _newerGroup));
        _memberships.Setup(x => x.FindInLatestKurinByAccountAsync(_userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Placed(_newerKurin, _newerGroup));
    }

    [Fact]
    public async Task InTheKurinJoinedFirst_TheViewerIsAMemberOfTheirGroupThere()
    {
        var viewer = await Access().BuildViewerAsync(_olderKurin);

        viewer.ViewerMemberKey.Should().Be(_memberKey);
        viewer.ViewerOwnGroupKey.Should().Be(_olderGroup);
        viewer.VisibilityGroupKeys.Should().BeEquivalentTo(new[] { _olderGroup });
    }

    [Fact]
    public async Task InTheKurinJoinedLast_TheViewerIsPlacedThere()
    {
        var viewer = await Access().BuildViewerAsync(_newerKurin);

        viewer.ViewerMemberKey.Should().Be(_memberKey);
        viewer.ViewerOwnGroupKey.Should().Be(_newerGroup);
    }

    [Fact]
    public async Task InAKurinTheyDoNotStandIn_TheViewerIsNobody()
    {
        var viewer = await Access().BuildViewerAsync(Guid.NewGuid());

        viewer.ViewerMemberKey.Should().BeNull();
        viewer.ViewerOwnGroupKey.Should().BeNull();
        viewer.VisibilityGroupKeys.Should().BeEmpty();
    }

    private KurinPerson Placed(Guid kurinKey, Guid groupKey) =>
        new(new MemberSummary(_memberKey, _userKey, "Ім'я", "Прізвище", "p@example.com", null), kurinKey, groupKey);

    private AgendaAccess Access()
    {
        var roles = new[] { SystemRoleNames.Member };
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(_userKey);
        user.SetupGet(x => x.Roles).Returns(roles);
        user.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string r) => roles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var scopes = ScopeFacts.Repository();
        var permissions = new CurrentPermissions(user.Object, new FixedKurinAccessPolicyProvider());
        var resourceAccess = new ResourceAccessService(UnitOfWorks.WithScopes(scopes.Object), user.Object, new PassThroughBackendCache(), permissions);
        return new AgendaAccess(user.Object, UnitOfWorks.WithScopes(scopes.Object), _memberships.Object, resourceAccess, permissions, new Mock<IKurinTagDirectory>().Object, new Mock<IOfficeDirectory>().Object, ScopeFacts.Reader(scopes.Object));
    }
}
