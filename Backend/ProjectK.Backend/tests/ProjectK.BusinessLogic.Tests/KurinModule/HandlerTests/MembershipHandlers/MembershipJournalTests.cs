using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Leave;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.MoveToGroup;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MembershipHandlers;

/// <summary>
/// Leaving a kurin and moving between its гуртки are recorded in that kurin's journal, with the
/// гуртки named — even when whoever acted is not scoped to that kurin.
/// </summary>
public class MembershipJournalTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly JournalRecorder _journal = new(Mock.Of<ICurrentUserContext>());

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Group _sokoly;
    private readonly Group _orly;
    private readonly Membership _membership;

    public MembershipJournalTests()
    {
        _sokoly = new Group("Соколи", _kurinKey);
        _orly = new Group("Орли", _kurinKey);
        _membership = new Membership { MemberKey = Guid.NewGuid(), KurinKey = _kurinKey, GroupKey = _sokoly.GroupKey, JoinedAtUtc = DateTime.UtcNow.AddYears(-1) };

        _unitOfWork.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        _unitOfWork.SetupGet(u => u.Groups).Returns(_groups.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _memberships.Setup(r => r.GetActiveAsync(_membership.MemberKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_membership);
        _groups.Setup(r => r.GetByKeyAsync(_sokoly.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_sokoly);
        _groups.Setup(r => r.GetByKeyAsync(_orly.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_orly);
        _members.Setup(d => d.FindAsync(_membership.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_membership.MemberKey, null, _kurinKey, _sokoly.GroupKey, "Олесь", "Юнак", "o@x", null));
        _journal.Knows(_membership.MemberKey, _kurinKey, _sokoly.GroupKey, "Олесь", "Юнак");
    }

    private MoveToGroupCommandHandler Move() =>
        new(_unitOfWork.Object, Mock.Of<IDomainEventPublisher>(), Mock.Of<IAccessChangeSignal>(), _members.Object, _journal.Audit);

    [Fact]
    public async Task Leaving_IsRecordedWithTheGroupTheyLeftFrom()
    {
        var handler = new LeaveKurinCommandHandler(_unitOfWork.Object, Mock.Of<IAccessChangeSignal>(), _members.Object, _journal.Audit);

        await handler.Handle(new LeaveKurinCommand(_membership.MemberKey, _kurinKey), default);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Membership);
        entry.Action.Should().Be(AuditActions.Membership.Left);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectKey.Should().Be(_membership.MembershipKey);
        entry.SubjectMemberKey.Should().Be(_membership.MemberKey);
        entry.SubjectGroupKey.Should().Be(_sokoly.GroupKey, "they are still placed there when it is recorded");
        entry.SubjectLabel.Should().Be("Олесь Юнак");
        entry.Changes.Should().Equal(
            new AuditChange("kind", "Youth", null),
            new AuditChange("group", "Соколи", null));
    }

    [Fact]
    public async Task Moving_IsRecordedFromGroupToGroup()
    {
        await Move().Handle(new MoveToGroupCommand(_membership.MemberKey, _kurinKey, _orly.GroupKey), default);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Membership.MovedToGroup);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectGroupKey.Should().Be(_sokoly.GroupKey);
        entry.Changes.Should().Equal(new AuditChange("group", "Соколи", "Орли"));
    }

    [Fact]
    public async Task TakingSomeoneOutOfTheirGroup_IsRecordedAsAMoveToNone()
    {
        await Move().Handle(new MoveToGroupCommand(_membership.MemberKey, _kurinKey, null), default);

        _journal.Entries.Single().Changes.Should().Equal(new AuditChange("group", "Соколи", null));
    }

    [Fact]
    public async Task MovingIntoTheGroupTheyAreIn_RecordsNothing()
    {
        await Move().Handle(new MoveToGroupCommand(_membership.MemberKey, _kurinKey, _sokoly.GroupKey), default);

        _journal.Entries.Should().BeEmpty();
    }
}
