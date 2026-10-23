using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Upsert;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;
using MentorAssignmentEntity = ProjectK.Common.Entities.KurinModule.MentorAssignment;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberAwardHandlers;

public class MemberAwardHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IMemberRepository> _memberRepositoryMock;
    private readonly Mock<IMemberAwardRepository> _memberAwardRepositoryMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IDomainEventPublisher> _eventsMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IUnitOfWork> _kurinDataMock = new();
    private readonly Mock<IMembershipRepository> _membershipsMock = new();
    private readonly Mock<IResourceAccessService> _access = new();

    private readonly UpsertMemberAwardCommandHandler _upsertHandler;
    private readonly ReviewMemberAwardCommandHandler _reviewHandler;
    private readonly DeleteMemberAwardCommandHandler _deleteHandler;

    public MemberAwardHandlerTests()
    {
        _unitOfWorkMock = new Mock<IMemberUnitOfWork>();
        _memberRepositoryMock = new Mock<IMemberRepository>();
        _memberAwardRepositoryMock = new Mock<IMemberAwardRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _eventsMock = new Mock<IDomainEventPublisher>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.SetupGet(x => x.Members).Returns(_memberRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(x => x.MemberAwards).Returns(_memberAwardRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _kurinDataMock.SetupGet(x => x.Memberships).Returns(_membershipsMock.Object);
        _membershipsMock
            .Setup(x => x.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Whatever the kurin's policy allows is the policy tests' business; here it allows all.
        _access.Setup(x => x.CheckCapabilityAsync(It.IsAny<KurinCapability>(), It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        _upsertHandler = new UpsertMemberAwardCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            _mapperMock.Object,
            _kurinDataMock.Object,
            _access.Object);
        _reviewHandler = new ReviewMemberAwardCommandHandler(
            _unitOfWorkMock.Object, _memberDirectory.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            _mapperMock.Object,
            _access.Object);
        _deleteHandler = new DeleteMemberAwardCommandHandler(
            _unitOfWorkMock.Object, _currentUserContextMock.Object, _memberDirectory.Object, _access.Object);
    }

    private void Refuse(KurinCapability capability)
    {
        _access.Setup(x => x.CheckCapabilityAsync(capability, It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Refused by the test."));
    }

    [Fact]
    public async Task Upsert_ShouldAnnounceThatAnAwardWasSubmitted()
    {
        var actorUserKey = Guid.NewGuid();
        var managerUserKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(kurinKey);
        var groupKey = Guid.NewGuid();

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(actorUserKey);
        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member
            {
                MemberKey = memberKey,
                FirstName = "Ivan",
                LastName = "Petrenko"
            });

        MemberAward? created = null;
        _memberAwardRepositoryMock
            .Setup(x => x.Create(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()))
            .Callback<MemberAward, CancellationToken>((award, _) => created = award);
        _mapperMock.Setup(m => m.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());

        var result = await _upsertHandler.Handle(new UpsertMemberAwardCommand
        {
            MemberKey = memberKey,
            Level = MemberAwardLevel.First,
            DateAcquired = DateTime.UtcNow
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        created.Should().NotBeNull();
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberAwardSubmitted>(raised =>
                raised.MemberAwardKey == created!.MemberAwardKey
                && raised.MemberKey == memberKey
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);

    }

    [Fact]
    public async Task Upsert_ShouldCreateNewAward_WhenKeyIsNotProvided()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(kurinKey);
        var userKey = _currentUserContextMock.Object.UserId!.Value;
        var dateAcquired = DateTime.UtcNow.AddDays(-1);

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        MemberAward? created = null;
        _memberAwardRepositoryMock
            .Setup(x => x.Create(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()))
            .Callback<MemberAward, CancellationToken>((award, _) => created = award);

        _mapperMock.Setup(m => m.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());

        var result = await _upsertHandler.Handle(new UpsertMemberAwardCommand
        {
            MemberKey = memberKey,
            Level = MemberAwardLevel.First,
            DateAcquired = dateAcquired,
            Note = "Test"
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        created.Should().NotBeNull();
        created!.MemberKey.Should().Be(memberKey);
        created.KurinKey.Should().Be(kurinKey);
        created.Level.Should().Be(MemberAwardLevel.First);
        created.DateAcquired.Should().Be(dateAcquired);
        created.Note.Should().Be("Test");
        created.Status.Should().Be(BadgeProgressStatus.Submitted);
        created.SubmittedByUserKey.Should().Be(userKey);
        created.SubmittedAtUtc.Should().NotBeNull();
        created.ReviewedByUserKey.Should().BeNull();
        created.ReviewedAtUtc.Should().BeNull();

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_ShouldUpdateExistingAward_WhenKeyIsProvided()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(kurinKey);
        var awardKey = Guid.NewGuid();
        var userKey = _currentUserContextMock.Object.UserId!.Value;
        var dateAcquired = DateTime.UtcNow.AddDays(-1);

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        var existingAward = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            KurinKey = kurinKey,
            Level = MemberAwardLevel.First,
            DateAcquired = DateTime.UtcNow.AddDays(-10),
            Note = "Old Note",
            Status = BadgeProgressStatus.Confirmed,
            ReviewedByUserKey = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAward);

        _mapperMock.Setup(m => m.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());
        _currentUserContextMock
            .SetupGet(x => x.Roles)
            .Returns(new[] { SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk) });

        var result = await _upsertHandler.Handle(new UpsertMemberAwardCommand
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Level = MemberAwardLevel.First,
            DateAcquired = dateAcquired,
            Note = "New Note"
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);

        _memberAwardRepositoryMock.Verify(x => x.Update(existingAward, It.IsAny<CancellationToken>()), Times.Once);

        existingAward.DateAcquired.Should().Be(dateAcquired);
        existingAward.Note.Should().Be("New Note");
        existingAward.Status.Should().Be(BadgeProgressStatus.Submitted);
        existingAward.SubmittedByUserKey.Should().Be(userKey);
        existingAward.SubmittedAtUtc.Should().NotBeNull();
        existingAward.ReviewedByUserKey.Should().BeNull();
        existingAward.ReviewedAtUtc.Should().BeNull();

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An edit sends the award back for review, so whoever may record awards could otherwise
    /// un-confirm a confirmed one just by saving it again.
    /// </summary>
    [Fact]
    public async Task Upsert_ShouldRefuseEditingConfirmedAward_WithoutRemoveAward()
    {
        Refuse(KurinCapability.RemoveAward);
        var memberKey = Guid.NewGuid();
        var awardKey = Guid.NewGuid();
        var reviewerKey = Guid.NewGuid();

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        var confirmed = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Level = MemberAwardLevel.First,
            Note = "Old Note",
            Status = BadgeProgressStatus.Confirmed,
            ReviewedByUserKey = reviewerKey
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmed);

        var result = await _upsertHandler.Handle(new UpsertMemberAwardCommand
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Level = MemberAwardLevel.Second,
            DateAcquired = DateTime.UtcNow.AddDays(-1),
            Note = "New Note"
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("ConfirmedAwardEditNotAllowed");
        confirmed.Status.Should().Be(BadgeProgressStatus.Confirmed);
        confirmed.Level.Should().Be(MemberAwardLevel.First);
        confirmed.ReviewedByUserKey.Should().Be(reviewerKey);
        _memberAwardRepositoryMock.Verify(x => x.Update(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_ShouldReturnNotFound_WhenAwardBelongsToAnotherMember()
    {
        var awardKey = Guid.NewGuid();
        var award = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = Guid.NewGuid(),
            Status = BadgeProgressStatus.Submitted
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(award);

        var result = await _reviewHandler.Handle(new ReviewMemberAwardCommand
        {
            MemberKey = Guid.NewGuid(),
            MemberAwardKey = awardKey,
            IsApproved = true
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        award.Status.Should().Be(BadgeProgressStatus.Submitted);
        _memberAwardRepositoryMock.Verify(x => x.Update(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenAwardBelongsToAnotherMember()
    {
        var awardKey = Guid.NewGuid();
        var award = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = Guid.NewGuid(),
            Status = BadgeProgressStatus.Submitted
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(award);
        _currentUserContextMock
            .SetupGet(x => x.Roles)
            .Returns(new[] { SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk) });

        var result = await _deleteHandler.Handle(
            new DeleteMemberAwardCommand { MemberKey = Guid.NewGuid(), MemberAwardKey = awardKey },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        _memberAwardRepositoryMock.Verify(x => x.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_ShouldReturnConflict_WhenTryingToRemoveConfirmedAward()
    {
        var awardKey = Guid.NewGuid();
        var existingAward = new MemberAward
        {
            MemberAwardKey = awardKey,
            Status = BadgeProgressStatus.Confirmed,
            ReviewedAtUtc = DateTime.UtcNow.AddDays(-1),
            ReviewedByUserKey = Guid.NewGuid()
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAward);

        var result = await _reviewHandler.Handle(new ReviewMemberAwardCommand
        {
            MemberAwardKey = awardKey,
            IsApproved = false
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        existingAward.Status.Should().Be(BadgeProgressStatus.Confirmed);
        _memberAwardRepositoryMock.Verify(x => x.Update(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_LinkedMemberAward_ShouldNotifyMemberOwner()
    {
        var actorUserKey = Guid.NewGuid();
        var memberUserKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var awardKey = Guid.NewGuid();
        var award = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Level = MemberAwardLevel.First,
            Status = BadgeProgressStatus.Submitted
        };

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(actorUserKey);
        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(award);
        _memberDirectory
            .Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberUserKey);
        _mapperMock.Setup(m => m.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());

        var result = await _reviewHandler.Handle(new ReviewMemberAwardCommand
        {
            MemberKey = memberKey,
            MemberAwardKey = awardKey,
            IsApproved = true
        }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberAwardReviewed>(raised =>
                raised.MemberAwardKey == awardKey
                && raised.MemberKey == memberKey
                && raised.MemberUserKey == memberUserKey
                && raised.IsApproved
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldWithdrawOwnSubmission_WithoutRemoveAward()
    {
        Refuse(KurinCapability.RemoveAward);
        var awardKey = Guid.NewGuid();
        var submitted = new MemberAward
        {
            MemberAwardKey = awardKey,
            Status = BadgeProgressStatus.Submitted,
            SubmittedByUserKey = _currentUserContextMock.Object.UserId
        };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submitted);

        var result = await _deleteHandler.Handle(new DeleteMemberAwardCommand { MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _memberAwardRepositoryMock.Verify(x => x.Delete(submitted, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Removing a confirmed award is the kurin's «remove awards» right — otherwise a member could
    /// erase a confirmed award with the Own grant they hold over their own awards.
    /// </summary>
    [Fact]
    public async Task Delete_ShouldRefuseConfirmedAward_WithoutRemoveAward()
    {
        Refuse(KurinCapability.RemoveAward);
        var awardKey = Guid.NewGuid();
        var confirmed = new MemberAward { MemberAwardKey = awardKey, Status = BadgeProgressStatus.Confirmed };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmed);

        var result = await _deleteHandler.Handle(new DeleteMemberAwardCommand { MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _memberAwardRepositoryMock.Verify(x => x.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldRemoveConfirmedAward_WithRemoveAward()
    {
        var awardKey = Guid.NewGuid();
        var confirmed = new MemberAward { MemberAwardKey = awardKey, Status = BadgeProgressStatus.Confirmed };

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmed);
        _currentUserContextMock
            .SetupGet(x => x.Roles)
            .Returns(new[] { SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk) });

        var result = await _deleteHandler.Handle(new DeleteMemberAwardCommand { MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _memberAwardRepositoryMock.Verify(x => x.Delete(confirmed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenItDoesNotExist()
    {
        var awardKey = Guid.NewGuid();

        _memberAwardRepositoryMock
            .Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberAward?)null);

        var result = await _deleteHandler.Handle(new DeleteMemberAwardCommand { MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        _memberAwardRepositoryMock.Verify(x => x.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldLetTheSubjectWithdrawAnUnconfirmedAward_SomeoneElseSubmitted()
    {
        Refuse(KurinCapability.RemoveAward);
        var memberKey = Guid.NewGuid();
        var awardKey = Guid.NewGuid();
        var submitted = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Status = BadgeProgressStatus.Submitted,
            SubmittedByUserKey = Guid.NewGuid()
        };
        _memberAwardRepositoryMock.Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>())).ReturnsAsync(submitted);
        _memberDirectory.Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currentUserContextMock.Object.UserId);

        var result = await _deleteHandler.Handle(
            new DeleteMemberAwardCommand { MemberKey = memberKey, MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _memberAwardRepositoryMock.Verify(x => x.Delete(submitted, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldRefuseSomeoneElsesSubmission_WithoutRemoveAward()
    {
        Refuse(KurinCapability.RemoveAward);
        var memberKey = Guid.NewGuid();
        var awardKey = Guid.NewGuid();
        var submitted = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Status = BadgeProgressStatus.Submitted,
            SubmittedByUserKey = Guid.NewGuid()
        };
        _memberAwardRepositoryMock.Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>())).ReturnsAsync(submitted);
        _memberDirectory.Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        var result = await _deleteHandler.Handle(
            new DeleteMemberAwardCommand { MemberKey = memberKey, MemberAwardKey = awardKey }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("AwardRemovalNotAllowed");
        _memberAwardRepositoryMock.Verify(x => x.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_ShouldRefuseALevelThePolicyKeeps()
    {
        Refuse(KurinCapability.ConfirmAward3);
        var memberKey = Guid.NewGuid();
        var awardKey = Guid.NewGuid();
        var award = new MemberAward
        {
            MemberAwardKey = awardKey,
            MemberKey = memberKey,
            Level = MemberAwardLevel.Third,
            Status = BadgeProgressStatus.Submitted
        };
        _memberAwardRepositoryMock.Setup(x => x.GetByKeyAsync(awardKey, It.IsAny<CancellationToken>())).ReturnsAsync(award);

        var result = await _reviewHandler.Handle(
            new ReviewMemberAwardCommand { MemberKey = memberKey, MemberAwardKey = awardKey, IsApproved = true }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("AwardLevelNotAllowed");
        award.Status.Should().Be(BadgeProgressStatus.Submitted);
    }
}
