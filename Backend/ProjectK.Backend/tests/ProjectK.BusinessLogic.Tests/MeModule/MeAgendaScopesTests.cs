using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>
/// The token knows one kurin; the dashboard has to see the person in every kurin they stand in, with
/// the rights they hold there and nowhere else.
/// </summary>
public class MeAgendaScopesTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid KurinA = Guid.NewGuid();
    private static readonly Guid KurinB = Guid.NewGuid();
    private static readonly Guid GroupA = Guid.NewGuid();
    private static readonly Guid LedInB = Guid.NewGuid();

    private readonly Mock<IOfficeDirectory> _offices = new();
    private readonly Mock<IResourceScopeReader> _scopeReader = new();
    private readonly List<MembershipRecord> _memberships = [];

    // The fallbacks go first so a test's own setup for one kurin wins over them.
    public MeAgendaScopesTests()
    {
        _offices.Setup(o => o.GetForAccountInKurinAsync(User, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _scopeReader.Setup(r => r.GetLedGroupKeysAsync(User, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private MeAgendaScopes Scopes(Guid? userKey = null)
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(userKey ?? User);
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetCurrentForAccountAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, KurinA, GroupA, "Оксана", "Паливода", "o@x", null));
        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(l => l.GetActiveLeadershipKeysForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        unitOfWork.SetupGet(u => u.KurinTags).Returns(TestHelpers.NoKurinTags.Repository());
        return new MeAgendaScopes(user.Object, memberships.Object, members.Object, _offices.Object, _scopeReader.Object, unitOfWork.Object);
    }

    private void StandIn(Guid kurin, int number, Guid? group, MembershipKind kind = MembershipKind.Youth) =>
        _memberships.Add(new MembershipRecord(Guid.NewGuid(), kurin, number, KurinBranch.UPYu, null, group, null, kind, DateTime.UtcNow, null));

    [Fact]
    public async Task AYouth_SeesTheirOwnGurtok_InEachKurin_AndNothingMore()
    {
        StandIn(KurinA, 1, GroupA);
        StandIn(KurinB, 7, null, MembershipKind.Staff);

        var contexts = await Scopes().BuildAsync(CancellationToken.None);

        contexts.Select(c => c.Membership.KurinNumber).Should().Equal(1, 7);
        contexts[0].Viewer.Should().BeEquivalentTo(new { KurinKey = KurinA, ViewerMemberKey = (Guid?)Person, CanSeeWholeKurin = false, IsLeadership = false });
        contexts[0].Viewer.VisibilityGroupKeys.Should().Equal(GroupA);
        contexts[1].Viewer.VisibilityGroupKeys.Should().BeEmpty();
    }

    // The Звʼязковий of B sees all of B — and only their гурток in A, where they are a youth.
    [Fact]
    public async Task AnOfficeInOneKurin_CountsThereOnly()
    {
        StandIn(KurinA, 1, GroupA);
        StandIn(KurinB, 7, null, MembershipKind.Staff);
        _offices.Setup(o => o.GetForAccountInKurinAsync(User, KurinB, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);

        var contexts = await Scopes().BuildAsync(CancellationToken.None);

        contexts[0].Viewer.CanSeeWholeKurin.Should().BeFalse();
        contexts[1].Viewer.Should().BeEquivalentTo(new { CanSeeWholeKurin = true, IsLeadership = true });
    }

    [Fact]
    public async Task AGurtkovyi_SeesTheGurtkyHeLeads()
    {
        StandIn(KurinB, 7, null, MembershipKind.Staff);
        _offices.Setup(o => o.GetForAccountInKurinAsync(User, KurinB, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk)]);
        _scopeReader.Setup(r => r.GetLedGroupKeysAsync(User, KurinB, It.IsAny<CancellationToken>())).ReturnsAsync([LedInB]);

        var contexts = await Scopes().BuildAsync(CancellationToken.None);

        contexts.Single().Viewer.VisibilityGroupKeys.Should().Equal(LedInB);
        contexts.Single().Viewer.CanSeeWholeKurin.Should().BeFalse();
    }

    [Fact]
    public async Task NobodySignedIn_HasNoKurins()
    {
        StandIn(KurinA, 1, GroupA);

        (await Scopes(Guid.Empty).BuildAsync(CancellationToken.None)).Should().BeEmpty();
    }
}
