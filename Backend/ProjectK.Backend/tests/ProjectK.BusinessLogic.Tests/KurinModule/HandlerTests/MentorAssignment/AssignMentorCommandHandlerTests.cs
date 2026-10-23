using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Assign;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;
using MentorAssignmentEntity = ProjectK.Common.Entities.KurinModule.MentorAssignment;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MentorAssignment;

public class AssignMentorCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IGroupRepository> _groupRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IMentorAssignmentRepository> _mentorAssignmentRepoMock = new();
    private readonly Mock<IBackendCache> _cacheMock = new();
    private readonly Mock<IAccessChangeSignal> _accessChanges = new();

    public AssignMentorCommandHandlerTests()
    {
        _uowMock.SetupGet(x => x.Groups).Returns(_groupRepoMock.Object);
        _uowMock.SetupGet(x => x.MentorAssignments).Returns(_mentorAssignmentRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private static (Group group, Member member) BuildFixture(Guid groupKey, Guid kurinKey, Guid mentorUserKey)
    {
        var group = new Group("G", kurinKey) { GroupKey = groupKey, KurinKey = kurinKey };
        var member = new Member { MemberKey = Guid.NewGuid(), UserKey = mentorUserKey, FirstName = "A", LastName = "B", Email = "a@b.com", PhoneNumber = "1", DateOfBirth = new DateOnly(2000, 1, 1) };
        return (group, member);
    }

    private void BelongsTo(Guid kurinKey, Guid memberKey, Guid userKey)
    {
        _memberDirectory
            .Setup(d => d.GetByKurinAsync(kurinKey, It.Is<MemberSelection>(s => s.AccountKeys.Contains(userKey)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberSummary(memberKey, userKey, kurinKey, null, "A", "B", "a@example.com", null)]);
    }

    [Fact]
    public async Task Handle_WhenTheirNewestMembershipIsInAnotherKurin_ShouldStillAssign()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var (group, member) = BuildFixture(groupKey, kurinKey, mentorUserKey);

        _groupRepoMock.Setup(x => x.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _mentorAssignmentRepoMock.Setup(x => x.GetSpecificAssignmentAsync(mentorUserKey, groupKey, It.IsAny<CancellationToken>())).ReturnsAsync((MentorAssignmentEntity?)null);
        // The summary names the kurin they joined last, not the one this гурток is in.
        _memberDirectory
            .Setup(d => d.FindByAccountAsync(mentorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(member.MemberKey, mentorUserKey, Guid.NewGuid(), null, "A", "B", "a@example.com", null));
        BelongsTo(kurinKey, member.MemberKey, mentorUserKey);
        var handler = new AssignMentorCommandHandler(_uowMock.Object, _memberDirectory.Object, _cacheMock.Object, _accessChanges.Object);

        var result = await handler.Handle(new AssignMentorCommand(mentorUserKey, groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _mentorAssignmentRepoMock.Verify(x => x.Create(It.IsAny<MentorAssignmentEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _accessChanges.Verify(s => s.MarkUser(mentorUserKey), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheyHaveNoMembershipInTheGroupsKurin_ShouldRefuse()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var (group, member) = BuildFixture(groupKey, kurinKey, mentorUserKey);

        _groupRepoMock.Setup(x => x.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _memberDirectory
            .Setup(d => d.FindByAccountAsync(mentorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(member.MemberKey, mentorUserKey, Guid.NewGuid(), null, "A", "B", "a@example.com", null));
        _memberDirectory
            .Setup(d => d.GetByKurinAsync(kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new AssignMentorCommandHandler(_uowMock.Object, _memberDirectory.Object, _cacheMock.Object, _accessChanges.Object);

        var result = await handler.Handle(new AssignMentorCommand(mentorUserKey, groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _mentorAssignmentRepoMock.Verify(x => x.Create(It.IsAny<MentorAssignmentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAssigned_ShouldCreateAssignmentAndSyncMemberRoles()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var (group, member) = BuildFixture(groupKey, kurinKey, mentorUserKey);

        _groupRepoMock.Setup(x => x.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        _mentorAssignmentRepoMock.Setup(x => x.GetSpecificAssignmentAsync(mentorUserKey, groupKey, It.IsAny<CancellationToken>())).ReturnsAsync((MentorAssignmentEntity?)null);
        _memberDirectory
            .Setup(d => d.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(member.MemberKey, mentorUserKey, kurinKey, groupKey, "A", "B", "a@example.com", null));
        BelongsTo(kurinKey, member.MemberKey, mentorUserKey);
        var handler = new AssignMentorCommandHandler(_uowMock.Object, _memberDirectory.Object, _cacheMock.Object, _accessChanges.Object);
        var result = await handler.Handle(new AssignMentorCommand(mentorUserKey, groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _mentorAssignmentRepoMock.Verify(x => x.Create(It.IsAny<MentorAssignmentEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        // Access comes from the assignment itself, read when the next token is minted.
        _cacheMock.Verify(x => x.Invalidate(It.IsAny<CachePolicy>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyActivelyAssigned_ShouldReturnConflictAndChangeNothing()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var (group, member) = BuildFixture(groupKey, kurinKey, mentorUserKey);

        _groupRepoMock.Setup(x => x.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        _mentorAssignmentRepoMock.Setup(x => x.GetSpecificAssignmentAsync(mentorUserKey, groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MentorAssignmentEntity { MentorAssignmentKey = Guid.NewGuid(), MentorUserKey = mentorUserKey, GroupKey = groupKey, AssignedAtUtc = DateTime.UtcNow });
        _memberDirectory
            .Setup(d => d.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(member.MemberKey, mentorUserKey, kurinKey, groupKey, "A", "B", "a@example.com", null));
        BelongsTo(kurinKey, member.MemberKey, mentorUserKey);
        var handler = new AssignMentorCommandHandler(_uowMock.Object, _memberDirectory.Object, _cacheMock.Object, _accessChanges.Object);
        var result = await handler.Handle(new AssignMentorCommand(mentorUserKey, groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _mentorAssignmentRepoMock.Verify(x => x.Create(It.IsAny<MentorAssignmentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.Invalidate(It.IsAny<CachePolicy>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPreviouslyRevoked_ShouldCreateNewAssignmentAndKeepTheOldOne()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var (group, member) = BuildFixture(groupKey, kurinKey, mentorUserKey);
        var revoked = new MentorAssignmentEntity
        {
            MentorAssignmentKey = Guid.NewGuid(),
            MentorUserKey = mentorUserKey,
            GroupKey = groupKey,
            AssignedAtUtc = DateTime.UtcNow.AddMonths(-3),
            RevokedAtUtc = DateTime.UtcNow.AddMonths(-1)
        };
        var revokedAt = revoked.RevokedAtUtc;

        _groupRepoMock.Setup(x => x.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _mentorAssignmentRepoMock.Setup(x => x.GetSpecificAssignmentAsync(mentorUserKey, groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revoked);
        _memberDirectory
            .Setup(d => d.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(member.MemberKey, mentorUserKey, kurinKey, groupKey, "A", "B", "a@example.com", null));
        BelongsTo(kurinKey, member.MemberKey, mentorUserKey);
        var handler = new AssignMentorCommandHandler(_uowMock.Object, _memberDirectory.Object, _cacheMock.Object, _accessChanges.Object);

        var result = await handler.Handle(new AssignMentorCommand(mentorUserKey, groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBe(revoked.MentorAssignmentKey);
        _mentorAssignmentRepoMock.Verify(x => x.Create(
            It.Is<MentorAssignmentEntity>(a => a.MentorUserKey == mentorUserKey && a.GroupKey == groupKey && a.RevokedAtUtc == null),
            It.IsAny<CancellationToken>()), Times.Once);
        revoked.RevokedAtUtc.Should().Be(revokedAt, "the earlier period stays in the history");
    }
}
