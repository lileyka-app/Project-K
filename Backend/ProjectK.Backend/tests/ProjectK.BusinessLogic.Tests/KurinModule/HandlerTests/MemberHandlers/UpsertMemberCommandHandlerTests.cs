using ProjectK.BusinessLogic.Tests.TestHelpers;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Account;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Photo;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

/// <summary>
/// The orchestrator owns the order of the three use cases and nothing else — these tests pin that
/// order and the two answers it gives on its own: a refused account and a stale-profile notice.
/// </summary>
public class UpsertMemberHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<IMemberUnitOfWork> _uowMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IAccountProvisioningService> _accountProvisioningMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserContextMock = new();
    private readonly Mock<IDomainEventPublisher> _eventsMock = new();
    private readonly UpsertMemberCommandHandler _handler;

    public UpsertMemberHandlerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, loggerFactory);

        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);
        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _accountProvisioningMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.Available);

        _handler = new UpsertMemberCommandHandler(
            _mediatorMock.Object,
            _uowMock.Object,
            mapperConfig.CreateMapper(),
            _accountProvisioningMock.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            FieldAccess.SeesEverything());
    }

    private Member GivenWrittenMember(bool isCreated, Guid? userKey = null)
    {
        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "Ivan",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "123",
            UserKey = userKey
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(member.MemberKey, isCreated, false, null)));

        _memberRepoMock.Setup(r => r.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        return member;
    }

    private void GivenAccountLink(MemberAccountLink? link) =>
        _memberRepoMock.Setup(r => r.GetAccountLinkAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

    [Fact]
    public async Task Handle_Create_ShouldWriteTheProfileAndAnswerCreated()
    {
        GivenAccountLink(null);
        var member = GivenWrittenMember(isCreated: true);

        var result = await _handler.Handle(
            new UpsertMemberCommand { KurinKey = Guid.NewGuid(), FirstName = "Ivan", LastName = "Petrenko" },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.FirstName.Should().Be("Ivan");
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheProfileWriteFails_ShouldPassTheFailureThrough()
    {
        GivenAccountLink(null);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MemberProfileWriteResult>.Failure(
                ResultType.BadRequest, "ContactInfoLinked", "nope"));

        var result = await _handler.Handle(
            new UpsertMemberCommand { KurinKey = Guid.NewGuid(), FirstName = "Ivan" },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("ContactInfoLinked");
    }

    [Fact]
    public async Task Handle_WithAccountRequested_ShouldProvisionAfterTheProfileIsWritten()
    {
        GivenAccountLink(null);
        var member = GivenWrittenMember(isCreated: true);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberInvitation>(ResultType.Success, new MemberInvitation(Guid.NewGuid(), true)));

        var result = await _handler.Handle(
            new UpsertMemberCommand
            {
                KurinKey = Guid.NewGuid(),
                CreateUserAccount = true,
                FirstName = "Ivan",
                Email = "ivan@example.com"
            },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.InvitationSent.Should().BeTrue();
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<ProvisionMemberAccountCommand>(c => c.MemberKey == member.MemberKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAccountRequested_WhenTheLetterDidNotGo_ShouldStillAnswerCreatedAndSaySo()
    {
        GivenAccountLink(null);
        GivenWrittenMember(isCreated: true);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberInvitation>(ResultType.Success, new MemberInvitation(Guid.NewGuid(), false)));

        var result = await _handler.Handle(
            new UpsertMemberCommand { KurinKey = Guid.NewGuid(), CreateUserAccount = true, Email = "ivan@example.com" },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.InvitationSent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTheMovedAccountsLetterDidNotGo_ShouldKeepTheEditAndSaySo()
    {
        GivenAccountLink(null);
        var member = GivenWrittenMember(isCreated: false, userKey: Guid.NewGuid());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(member.MemberKey, false, false, null, AccountEmailToFollow: true)));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<MoveMemberAccountEmailCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Guid>.Failure(
                ResultType.InternalServerError, ResendMemberInvitationCommandHandler.InvitationNotSent, "not sent"));

        var result = await _handler.Handle(
            new UpsertMemberCommand { MemberKey = member.MemberKey, KurinKey = Guid.NewGuid(), Email = "fixed@example.com" },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.InvitationSent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTheAddressChangedUnderAnAccount_ShouldMoveTheAccountAfterTheProfile()
    {
        GivenAccountLink(null);
        var member = GivenWrittenMember(isCreated: false, userKey: Guid.NewGuid());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(member.MemberKey, false, false, null, AccountEmailToFollow: true)));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<MoveMemberAccountEmailCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Guid>(ResultType.Success, member.UserKey!.Value));

        var result = await _handler.Handle(
            new UpsertMemberCommand { MemberKey = member.MemberKey, KurinKey = Guid.NewGuid(), Email = "fixed@example.com" },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<MoveMemberAccountEmailCommand>(c => c.MemberKey == member.MemberKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheAddressDidNotChange_ShouldLeaveTheAccountAlone()
    {
        GivenAccountLink(null);
        GivenWrittenMember(isCreated: false, userKey: Guid.NewGuid());

        await _handler.Handle(
            new UpsertMemberCommand { KurinKey = Guid.NewGuid(), FirstName = "Ivan" },
            CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<MoveMemberAccountEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAccountRequested_WhenTheAddressIsTaken_ShouldConflictBeforeWritingAnything()
    {
        GivenAccountLink(null);
        _accountProvisioningMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.EmailTaken);

        var result = await _handler.Handle(
            new UpsertMemberCommand
            {
                KurinKey = Guid.NewGuid(),
                CreateUserAccount = true,
                FirstName = "Ivan",
                Email = "taken@example.com"
            },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAccountRequested_WhenTheMemberAlreadyHasOne_ShouldConflict()
    {
        var memberKey = Guid.NewGuid();
        GivenAccountLink(new MemberAccountLink(memberKey, Guid.NewGuid()));
        _currentUserContextMock.Setup(x => x.IsInRole("Admin")).Returns(true);
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { "Admin" });

        var result = await _handler.Handle(
            new UpsertMemberCommand
            {
                MemberKey = memberKey,
                KurinKey = Guid.NewGuid(),
                CreateUserAccount = true,
                FirstName = "Ivan",
                Email = "ivan@example.com"
            },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAccountRequested_BySomeoneWithoutLeadership_ShouldIgnoreTheRequest()
    {
        var memberKey = Guid.NewGuid();
        GivenAccountLink(new MemberAccountLink(memberKey, null));
        var member = GivenWrittenMember(isCreated: false);

        await _handler.Handle(
            new UpsertMemberCommand
            {
                MemberKey = memberKey,
                KurinKey = Guid.NewGuid(),
                CreateUserAccount = true,
                FirstName = "Ivan",
                Email = "ivan@example.com"
            },
            CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAPhoto_ShouldHandItToThePhotoUseCase()
    {
        GivenAccountLink(null);
        var member = GivenWrittenMember(isCreated: false);
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SetMemberPhotoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<string?>(ResultType.Success, "new.png"));

        await _handler.Handle(
            new UpsertMemberCommand
            {
                MemberKey = member.MemberKey,
                KurinKey = Guid.NewGuid(),
                FirstName = "Ivan",
                BlobContent = content,
                BlobFileName = "new.png"
            },
            CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(
                It.Is<SetMemberPhotoCommand>(c => c.MemberKey == member.MemberKey && c.FileName == "new.png"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAVerifiedProfileWentStale_ShouldRaiseItForTheProvid()
    {
        var actorUserKey = Guid.NewGuid();
        var memberUserKey = Guid.NewGuid();
        _currentUserContextMock.SetupGet(x => x.UserId).Returns(actorUserKey);
        GivenAccountLink(new MemberAccountLink(Guid.NewGuid(), memberUserKey));

        var member = GivenWrittenMember(isCreated: false, userKey: memberUserKey);
        member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedStale;

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(member.MemberKey, false, true, null)));

        await _handler.Handle(
            new UpsertMemberCommand { MemberKey = member.MemberKey, KurinKey = Guid.NewGuid(), FirstName = "Changed" },
            CancellationToken.None);

        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberProfileWentStale>(raised =>
                raised.MemberKey == member.MemberKey
                && raised.MemberName == $"{member.FirstName} {member.LastName}".Trim()
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
