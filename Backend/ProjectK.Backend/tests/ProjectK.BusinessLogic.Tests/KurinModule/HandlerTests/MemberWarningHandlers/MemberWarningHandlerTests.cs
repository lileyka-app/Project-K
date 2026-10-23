using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Assign;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Cancel;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberWarningHandlers;

public class MemberWarningHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMemberRepository> _memberRepositoryMock;
    private readonly Mock<IMemberWarningRepository> _memberWarningRepositoryMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IDomainEventPublisher> _eventsMock;
    private readonly Mock<AutoMapper.IMapper> _mapperMock;
    private readonly Mock<IResourceAccessService> _access = new();

    private readonly AssignMemberWarningCommandHandler _assignHandler;
    private readonly FixedTimeProvider _clock;
    private readonly CancelMemberWarningCommandHandler _cancelHandler;

    public MemberWarningHandlerTests()
    {
        _unitOfWorkMock = new Mock<IMemberUnitOfWork>();
        _memberRepositoryMock = new Mock<IMemberRepository>();
        _memberWarningRepositoryMock = new Mock<IMemberWarningRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _eventsMock = new Mock<IDomainEventPublisher>();
        _mapperMock = new Mock<AutoMapper.IMapper>();

        _unitOfWorkMock.SetupGet(x => x.Members).Returns(_memberRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(x => x.MemberWarnings).Returns(_memberWarningRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        // A впорядник by default: every level is his to give, someone else's is not his to cancel.
        _access.Setup(x => x.CheckCapabilityAsync(It.IsAny<KurinCapability>(), It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        _access.Setup(x => x.CheckCapabilityAsync(KurinCapability.CancelWarning, It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Not his to cancel."));

        _assignHandler = new AssignMemberWarningCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            _mapperMock.Object,
            _access.Object);
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero));
        _cancelHandler = new CancelMemberWarningCommandHandler(_unitOfWorkMock.Object, _currentUserContextMock.Object, _mapperMock.Object, _clock, _access.Object);
    }

    [Fact]
    public async Task Assign_Level1_ShouldCreateWarningWithThreeMonthsExpiry()
    {
        var memberKey = Guid.NewGuid();
        var userKey = _currentUserContextMock.Object.UserId!.Value;

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        _memberWarningRepositoryMock
            .Setup(x => x.GetActiveByMemberKeyAsync(memberKey, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        MemberWarning? created = null;
        _memberWarningRepositoryMock
            .Setup(x => x.Create(It.IsAny<MemberWarning>(), It.IsAny<CancellationToken>()))
            .Callback<MemberWarning, CancellationToken>((warning, _) => created = warning);

        var before = DateTime.UtcNow;
        var result = await _assignHandler.Handle(new AssignMemberWarningCommand(memberKey, MemberWarningLevel.Level1), CancellationToken.None);
        var after = DateTime.UtcNow;

        result.Type.Should().Be(ResultType.Created);
        created.Should().NotBeNull();
        created!.MemberKey.Should().Be(memberKey);
        created.Level.Should().Be(MemberWarningLevel.Level1);
        created.IssuedByUserKey.Should().Be(userKey);
        created.IssuedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        created.ExpiresAtUtc.Should().BeOnOrAfter(before.AddMonths(3)).And.BeOnOrBefore(after.AddMonths(3));

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_Level2_WithoutActiveLevel1_ShouldReturnConflict()
    {
        var memberKey = Guid.NewGuid();

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        _memberWarningRepositoryMock
            .Setup(x => x.GetActiveByMemberKeyAsync(memberKey, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _assignHandler.Handle(new AssignMemberWarningCommand(memberKey, MemberWarningLevel.Level2), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _memberWarningRepositoryMock.Verify(x => x.Create(It.IsAny<MemberWarning>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assign_Level2_ShouldKeepLevel1ActiveAndCreateLevel2()
    {
        var memberKey = Guid.NewGuid();
        var level1AuthorKey = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey });

        var activeWarning = new MemberWarning
        {
            MemberWarningKey = Guid.NewGuid(),
            MemberKey = memberKey,
            Level = MemberWarningLevel.Level1,
            IssuedAtUtc = now.AddDays(-10),
            ExpiresAtUtc = now.AddDays(10),
            IssuedByUserKey = level1AuthorKey
        };

        _memberWarningRepositoryMock
            .Setup(x => x.GetActiveByMemberKeyAsync(memberKey, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeWarning]);

        MemberWarning? created = null;
        _memberWarningRepositoryMock
            .Setup(x => x.Create(It.IsAny<MemberWarning>(), It.IsAny<CancellationToken>()))
            .Callback<MemberWarning, CancellationToken>((warning, _) => created = warning);

        var result = await _assignHandler.Handle(new AssignMemberWarningCommand(memberKey, MemberWarningLevel.Level2), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        activeWarning.RevokedAtUtc.Should().BeNull();
        activeWarning.RevokedByUserKey.Should().BeNull();
        activeWarning.IssuedByUserKey.Should().Be(level1AuthorKey);
        created.Should().NotBeNull();
        created!.Level.Should().Be(MemberWarningLevel.Level2);
        created.MemberKey.Should().Be(memberKey);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_LinkedMember_ShouldNotifyMemberOwner()
    {
        var memberKey = Guid.NewGuid();
        var memberUserKey = Guid.NewGuid();
        var actorUserKey = Guid.NewGuid();

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(actorUserKey);
        _memberRepositoryMock
            .Setup(x => x.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = memberKey, UserKey = memberUserKey });
        _memberWarningRepositoryMock
            .Setup(x => x.GetActiveByMemberKeyAsync(memberKey, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        MemberWarning? created = null;
        _memberWarningRepositoryMock
            .Setup(x => x.Create(It.IsAny<MemberWarning>(), It.IsAny<CancellationToken>()))
            .Callback<MemberWarning, CancellationToken>((warning, _) => created = warning);

        var result = await _assignHandler.Handle(new AssignMemberWarningCommand(memberKey, MemberWarningLevel.Level1), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        created.Should().NotBeNull();
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberWarningAssigned>(raised =>
                raised.MemberWarningKey == created!.MemberWarningKey
                && raised.MemberKey == memberKey
                && raised.MemberUserKey == memberUserKey
                && raised.Level == MemberWarningLevel.Level1
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldRefuse_WhenTheWarningHasAlreadyExpired()
    {
        // The clock is fixed, so the expiry branch is reachable without dating the fixture
        // relative to the real "now".
        var memberKey = Guid.NewGuid();
        var warningKey = Guid.NewGuid();
        var userKey = _currentUserContextMock.Object.UserId!.Value;
        var now = _clock.GetUtcNow().UtcDateTime;

        _memberWarningRepositoryMock
            .Setup(x => x.GetByKeyAsync(warningKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberWarning
            {
                MemberWarningKey = warningKey,
                MemberKey = memberKey,
                Level = MemberWarningLevel.Level1,
                IssuedAtUtc = now.AddMonths(-4),
                ExpiresAtUtc = now.AddMinutes(-1),
                IssuedByUserKey = userKey
            });

        var result = await _cancelHandler.Handle(new CancelMemberWarningCommand(memberKey, warningKey), CancellationToken.None);

        result.Type.Should().NotBe(ResultType.Success);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ShouldReturnForbidden_WhenUserIsNotAuthor()
    {
        var memberKey = Guid.NewGuid();
        var warningKey = Guid.NewGuid();

        _memberWarningRepositoryMock
            .Setup(x => x.GetByKeyAsync(warningKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberWarning
            {
                MemberWarningKey = warningKey,
                MemberKey = memberKey,
                Level = MemberWarningLevel.Level1,
                IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                IssuedByUserKey = Guid.NewGuid()
            });

        var result = await _cancelHandler.Handle(new CancelMemberWarningCommand(memberKey, warningKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ShouldRevokeWarning_WhenUserIsAuthor()
    {
        var memberKey = Guid.NewGuid();
        var warningKey = Guid.NewGuid();
        var userKey = _currentUserContextMock.Object.UserId!.Value;

        var warning = new MemberWarning
        {
            MemberWarningKey = warningKey,
            MemberKey = memberKey,
            Level = MemberWarningLevel.Level1,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            IssuedByUserKey = userKey
        };

        _memberWarningRepositoryMock
            .Setup(x => x.GetByKeyAsync(warningKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warning);

        var result = await _cancelHandler.Handle(new CancelMemberWarningCommand(memberKey, warningKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        warning.RevokedAtUtc.Should().NotBeNull();
        warning.RevokedByUserKey.Should().Be(userKey);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
