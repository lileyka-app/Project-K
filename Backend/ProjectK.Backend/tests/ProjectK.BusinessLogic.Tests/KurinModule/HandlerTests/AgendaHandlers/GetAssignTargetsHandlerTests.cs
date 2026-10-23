using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class GetAssignTargetsHandlerTests
{
    private const CapabilityReach K = CapabilityReach.Kurin;
    private const CapabilityReach G = CapabilityReach.OwnGroup;
    private const CapabilityReach N = CapabilityReach.None;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<IGroupRepository> _groupRepo = new();
    private readonly Mock<ILeadershipRepository> _leadershipRepo = new();
    private readonly GetAssignTargetsQueryHandler _handler;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _g1 = Guid.NewGuid();
    private readonly Guid _g2 = Guid.NewGuid();
    private readonly Guid _viewerUserKey = Guid.NewGuid();
    private readonly Guid _inG1 = Guid.NewGuid();
    private readonly Guid _inG2 = Guid.NewGuid();
    private readonly Guid _viewerMemberKey = Guid.NewGuid();
    private readonly Guid _kv = Guid.NewGuid();
    private readonly Guid _g1Provid = Guid.NewGuid();
    private readonly Guid _g2Provid = Guid.NewGuid();

    public GetAssignTargetsHandlerTests()
    {
        var groups = new List<Group>
        {
            new("Gurtok 1", _kurinKey) { GroupKey = _g1 },
            new("Gurtok 2", _kurinKey) { GroupKey = _g2 }
        };
        var members = new List<MemberSummary>
        {
            new(_inG1, null, _kurinKey, _g1, "A", "One", "a@example.com", null),
            new(_inG2, null, _kurinKey, _g2, "B", "Two", "b@example.com", null),
            new(_viewerMemberKey, _viewerUserKey, _kurinKey, _g2, "C", "Viewer", "c@example.com", null)
        };

        _uow.Setup(u => u.Groups).Returns(_groupRepo.Object);
        _uow.Setup(u => u.Leaderships).Returns(_leadershipRepo.Object);
        _groupRepo.Setup(r => r.GetAllAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        _memberDirectory.Setup(r => r.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(members);
        _leadershipRepo.Setup(r => r.GetLeadershipRefsForKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LeadershipRef(_kv, LeadershipType.KV, null),
                new LeadershipRef(_g1Provid, LeadershipType.Group, _g1),
                new LeadershipRef(_g2Provid, LeadershipType.Group, _g2)
            });

        _handler = new GetAssignTargetsQueryHandler(_uow.Object, _memberDirectory.Object, _access.Object);
    }

    private void Reach(CapabilityReach kurin, CapabilityReach groups, CapabilityReach members, CapabilityReach provid, params Guid[] led) =>
        _access.Setup(a => a.GetAssignReachAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaAssignReach(kurin, groups, members, provid, led.ToHashSet(), _viewerUserKey));

    [Fact]
    public async Task Handle_ForWholeKurinReach_OffersEverything()
    {
        Reach(K, K, K, K);

        var result = await _handler.Handle(new GetAssignTargetsQuery(_kurinKey), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.CanTargetKurin.Should().BeTrue();
        result.Data.KurinLeaderships.Should().ContainSingle().Which.CanTarget.Should().BeTrue();
        result.Data.Groups.Should().HaveCount(2);
        result.Data.Groups.Should().OnlyContain(g => g.CanTargetGroup && g.Leadership!.CanTarget && g.Members.All(m => m.CanTarget));
    }

    [Fact]
    public async Task Handle_ForOwnGroupReach_OffersOnlyTheLedGroup_ItsProvidAndPeople_AndTheViewerHimself()
    {
        Reach(N, G, G, G, _g1);

        var result = await _handler.Handle(new GetAssignTargetsQuery(_kurinKey), default);

        result.Data!.CanTargetKurin.Should().BeFalse();
        result.Data.KurinLeaderships.Should().ContainSingle().Which.CanTarget.Should().BeFalse();

        var led = result.Data.Groups.Single(g => g.GroupKey == _g1);
        led.CanTargetGroup.Should().BeTrue();
        led.Leadership!.CanTarget.Should().BeTrue();
        led.Members.Should().ContainSingle().Which.CanTarget.Should().BeTrue();

        // His own гурток is listed only for himself: an own-group grant always covers one's own record.
        var own = result.Data.Groups.Single(g => g.GroupKey == _g2);
        own.CanTargetGroup.Should().BeFalse();
        own.Leadership!.CanTarget.Should().BeFalse();
        own.Members.Single(m => m.MemberKey == _viewerMemberKey).CanTarget.Should().BeTrue();
        own.Members.Single(m => m.MemberKey == _inG2).CanTarget.Should().BeFalse();
    }

    // Each kind of target is its own row: people kurin-wide without any гурток or провід.
    [Fact]
    public async Task Handle_ForMembersOnly_ListsEveryGroupForItsPeople_WithNothingElsePickable()
    {
        Reach(N, N, K, N);

        var result = await _handler.Handle(new GetAssignTargetsQuery(_kurinKey), default);

        result.Data!.CanTargetKurin.Should().BeFalse();
        result.Data.Groups.Should().HaveCount(2);
        result.Data.Groups.Should().OnlyContain(g => !g.CanTargetGroup && !g.Leadership!.CanTarget && g.Members.All(m => m.CanTarget));
    }

    [Fact]
    public async Task Handle_LeavesOutAGroupWithNothingToPick()
    {
        Reach(N, G, N, N, _g1);

        var result = await _handler.Handle(new GetAssignTargetsQuery(_kurinKey), default);

        result.Data!.Groups.Should().ContainSingle().Which.GroupKey.Should().Be(_g1);
        result.Data.Groups.Single().Members.Should().OnlyContain(m => !m.CanTarget);
    }

    [Fact]
    public async Task Handle_WhenThePolicyLetsTheUserAssignNothing_IsForbidden()
    {
        Reach(N, N, N, N);

        var result = await _handler.Handle(new GetAssignTargetsQuery(_kurinKey), default);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("AGENDA_AUTHOR_FORBIDDEN");
    }
}
