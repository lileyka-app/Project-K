using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Revoke;
using ProjectK.BusinessLogic.Services.Caching;
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
using MentorAssignmentEntity = ProjectK.Common.Entities.KurinModule.MentorAssignment;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MentorAssignment;

public class RevokeMentorCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IMentorAssignmentRepository> _assignments = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly JournalRecorder _journal = new(Mock.Of<ICurrentUserContext>());
    private readonly RevokeMentorCommandHandler _handler;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _mentorUserKey = Guid.NewGuid();
    private readonly Guid _mentorMemberKey = Guid.NewGuid();
    private readonly Group _group;
    private readonly MentorAssignmentEntity _assignment;

    public RevokeMentorCommandHandlerTests()
    {
        _group = new Group("Соколи", _kurinKey);
        _assignment = new MentorAssignmentEntity
        {
            MentorAssignmentKey = Guid.NewGuid(),
            MentorUserKey = _mentorUserKey,
            GroupKey = _group.GroupKey,
            AssignedAtUtc = DateTime.UtcNow.AddMonths(-3)
        };

        _uow.SetupGet(u => u.Groups).Returns(_groups.Object);
        _uow.SetupGet(u => u.MentorAssignments).Returns(_assignments.Object);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _groups.Setup(r => r.GetByKeyAsync(_group.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_group);
        _assignments.Setup(r => r.GetSpecificAssignmentAsync(_mentorUserKey, _group.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_assignment);
        _members.Setup(d => d.FindByAccountAsync(_mentorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_mentorMemberKey, _mentorUserKey, _kurinKey, null, "Марта", "Впорядниця", "m@x", null));

        _handler = new RevokeMentorCommandHandler(_uow.Object, Mock.Of<IBackendCache>(), Mock.Of<IAccessChangeSignal>(), _members.Object, _journal.Audit);
    }

    [Fact]
    public async Task Revoking_IsRecordedAboutTheMentor_WithTheGroupTheyLeft()
    {
        var result = await _handler.Handle(new RevokeMentorCommand(_mentorUserKey, _group.GroupKey), default);

        result.Type.Should().Be(ResultType.Success);
        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Office);
        entry.Action.Should().Be(AuditActions.Office.MentorRevoked);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectKey.Should().Be(_assignment.MentorAssignmentKey);
        entry.SubjectMemberKey.Should().Be(_mentorMemberKey);
        entry.SubjectLabel.Should().Be("Впорядник");
        entry.Changes.Should().Equal(new AuditChange("group", "Соколи", null));
    }

    [Fact]
    public async Task RevokingWhatIsAlreadyRevoked_RecordsNothing()
    {
        _assignment.RevokedAtUtc = DateTime.UtcNow.AddDays(-1);

        await _handler.Handle(new RevokeMentorCommand(_mentorUserKey, _group.GroupKey), default);

        _journal.Entries.Should().BeEmpty();
    }
}
