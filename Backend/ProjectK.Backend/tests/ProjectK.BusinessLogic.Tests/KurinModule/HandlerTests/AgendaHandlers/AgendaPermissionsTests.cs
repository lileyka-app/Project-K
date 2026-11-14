using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class AgendaPermissionsTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _leadershipKey = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();

    private AgendaItem ItemAssignedTo(AgendaTargetType type, Guid key, Guid? createdBy = null) => new()
    {
        AgendaItemKey = Guid.NewGuid(),
        KurinKey = _kurinKey,
        Kind = AgendaItemKind.Event,
        CreatedByUserKey = createdBy ?? Guid.NewGuid(),
        Assignments = new List<AgendaAssignment> { new() { TargetType = type, TargetKey = key } }
    };

    private AgendaViewerContext Viewer(bool wholeKurin = false, bool leadership = false, Guid[]? groups = null,
        Guid[]? leaderships = null, Guid? memberKey = null, Guid? ownGroup = null) =>
        new(_kurinKey, _userKey, memberKey, ownGroup, groups ?? Array.Empty<Guid>(),
            leaderships ?? Array.Empty<Guid>(), wholeKurin, leadership);

    [Fact]
    public void IsVisibleTo_WholeKurinViewer_SeesAnything()
    {
        var item = ItemAssignedTo(AgendaTargetType.Member, Guid.NewGuid());
        AgendaPermissions.IsVisibleTo(item, Viewer(wholeKurin: true)).Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_KurinAssignment_VisibleToEveryone()
    {
        var item = ItemAssignedTo(AgendaTargetType.Kurin, _kurinKey);
        AgendaPermissions.IsVisibleTo(item, Viewer()).Should().BeTrue();
    }

    // The author follows what they raised even when it went to someone else; it is visible, but not
    // addressed to them, which is what the board draws differently.
    [Fact]
    public void IsVisibleTo_Author_SeesTheItemTheyRaised_ButItIsNotAddressedToThem()
    {
        var item = ItemAssignedTo(AgendaTargetType.Member, Guid.NewGuid(), createdBy: _userKey);

        AgendaPermissions.IsVisibleTo(item, Viewer()).Should().BeTrue();
        AgendaVisibility.IsAddressedTo(item, Viewer().ToScope()).Should().BeFalse();
        AgendaPermissions.IsVisibleTo(item, Viewer() with { ViewerUserKey = Guid.NewGuid() }).Should().BeFalse();
    }

    [Fact]
    public void IsVisibleTo_GroupAssignment_VisibleOnlyToThatGroup()
    {
        var item = ItemAssignedTo(AgendaTargetType.Group, _groupKey);
        AgendaPermissions.IsVisibleTo(item, Viewer(groups: new[] { _groupKey })).Should().BeTrue();
        AgendaPermissions.IsVisibleTo(item, Viewer(groups: new[] { Guid.NewGuid() })).Should().BeFalse();
    }

    [Fact]
    public void IsVisibleTo_LeadershipAssignment_VisibleToOfficeHolder()
    {
        var item = ItemAssignedTo(AgendaTargetType.Leadership, _leadershipKey);
        AgendaPermissions.IsVisibleTo(item, Viewer(leaderships: new[] { _leadershipKey })).Should().BeTrue();
        AgendaPermissions.IsVisibleTo(item, Viewer()).Should().BeFalse();
    }

    [Fact]
    public void IsVisibleTo_MemberAssignment_VisibleOnlyToThatMember()
    {
        var item = ItemAssignedTo(AgendaTargetType.Member, _memberKey);
        AgendaPermissions.IsVisibleTo(item, Viewer(memberKey: _memberKey)).Should().BeTrue();
        AgendaPermissions.IsVisibleTo(item, Viewer(memberKey: Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void CanManage_CreatorOrWholeKurin_True_PlainAssignee_False()
    {
        var item = ItemAssignedTo(AgendaTargetType.Group, _groupKey, createdBy: _userKey);
        AgendaPermissions.CanManage(item, Viewer()).Should().BeTrue("the creator may manage");

        var other = ItemAssignedTo(AgendaTargetType.Group, _groupKey);
        AgendaPermissions.CanManage(other, Viewer(wholeKurin: true)).Should().BeTrue();
        AgendaPermissions.CanManage(other, Viewer(memberKey: _memberKey, ownGroup: _groupKey)).Should().BeFalse("a plain member is not a manager");
    }

    [Fact]
    public void CanManage_GroupLeaderOfTheAssignedGroup_True()
    {
        var item = ItemAssignedTo(AgendaTargetType.Group, _groupKey);
        AgendaPermissions.CanManage(item, Viewer(leadership: true, groups: new[] { _groupKey })).Should().BeTrue();
        AgendaPermissions.CanManage(item, Viewer(leadership: true, groups: new[] { Guid.NewGuid() })).Should().BeFalse();
    }

    [Fact]
    public void CanChangeStatus_AssigneeMayChange_EvenThoughNotManager()
    {
        var item = ItemAssignedTo(AgendaTargetType.Member, _memberKey);
        var assignee = Viewer(memberKey: _memberKey);
        AgendaPermissions.CanManage(item, assignee).Should().BeFalse();
        AgendaPermissions.CanChangeStatus(item, assignee).Should().BeTrue();
    }
}
