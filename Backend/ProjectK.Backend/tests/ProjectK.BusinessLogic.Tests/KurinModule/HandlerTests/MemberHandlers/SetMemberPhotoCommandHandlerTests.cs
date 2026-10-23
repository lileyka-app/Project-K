using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Photo;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class SetMemberPhotoCommandHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IPhotoService> _photoServiceMock = new();
    private readonly SetMemberPhotoCommandHandler _handler;
    private readonly JournalRecorder _journal;

    public SetMemberPhotoCommandHandlerTests()
    {
        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.KurinKey).Returns(Guid.NewGuid());
        _journal = new JournalRecorder(user.Object);
        _handler = new SetMemberPhotoCommandHandler(_uowMock.Object, _photoServiceMock.Object, _journal.Audit);
    }

    private Member GivenMember(string? blobName = "old.png")
    {
        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "A",
            LastName = "B",
            Email = "a@example.com",
            PhoneNumber = "1",
            ProfilePhotoBlobName = blobName
        };

        _memberRepoMock.Setup(r => r.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        return member;
    }

    [Fact]
    public async Task Handle_Upload_ShouldReplacePhotoAndDeleteThePreviousBlob()
    {
        var member = GivenMember();
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _photoServiceMock
            .Setup(p => p.UploadPhotoAsync(It.IsAny<Stream>(), "new.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("new.png", "TEST_URL"));

        var result = await _handler.Handle(
            new SetMemberPhotoCommand(member.MemberKey, content, "new.png", Remove: false),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.ProfilePhotoBlobName.Should().Be("new.png");
        _photoServiceMock.Verify(p => p.DeletePhotoAsync("old.png", It.IsAny<CancellationToken>()), Times.Once);
        _memberRepoMock.Verify(r => r.Update(member, It.IsAny<CancellationToken>()), Times.Once);

        // That it changed, never where it is stored.
        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Member);
        entry.Action.Should().Be(AuditActions.Member.PhotoChanged);
        entry.SubjectMemberKey.Should().Be(member.MemberKey);
        entry.SubjectLabel.Should().Be("A B");
        entry.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Upload_OnVerifiedProfile_ShouldSendItBackForVerification()
    {
        var member = GivenMember();
        member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        using var content = new MemoryStream(new byte[] { 1 });

        _photoServiceMock
            .Setup(p => p.UploadPhotoAsync(It.IsAny<Stream>(), "new.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("new.png", "TEST_URL"));

        await _handler.Handle(
            new SetMemberPhotoCommand(member.MemberKey, content, "new.png", Remove: false),
            CancellationToken.None);

        member.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedStale);
        _journal.Entries.Single().Changes.Should().Equal(new AuditChange("profileVerification", "VerifiedCurrent", "VerifiedStale"));
    }

    [Fact]
    public async Task Handle_Remove_ShouldClearThePhotoAndDeleteTheBlobOnce()
    {
        var member = GivenMember();

        var result = await _handler.Handle(
            new SetMemberPhotoCommand(member.MemberKey, Content: null, FileName: null, Remove: true),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.ProfilePhotoBlobName.Should().BeNull();
        _photoServiceMock.Verify(p => p.DeletePhotoAsync("old.png", It.IsAny<CancellationToken>()), Times.Once);
        _journal.Entries.Single().Action.Should().Be(AuditActions.Member.PhotoRemoved);
    }

    [Fact]
    public async Task Handle_RemoveWithoutAPhoto_ShouldDoNothing()
    {
        var member = GivenMember(blobName: null);

        var result = await _handler.Handle(
            new SetMemberPhotoCommand(member.MemberKey, Content: null, FileName: null, Remove: true),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _photoServiceMock.Verify(p => p.DeletePhotoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownMember_ShouldReturnNotFound()
    {
        _memberRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member)null!);

        var result = await _handler.Handle(
            new SetMemberPhotoCommand(Guid.NewGuid(), Content: null, FileName: null, Remove: true),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
    }
}
