using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Account;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class ProvisionMemberAccountCommandHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IUnitOfWork> _kurinDataMock = new();
    private readonly Mock<IMembershipRepository> _membershipsMock = new();
    private readonly Mock<IKurinRepository> _kurinsMock = new();
    private readonly Mock<IAccountProvisioningService> _accountProvisioningMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly ProvisionMemberAccountCommandHandler _handler;

    public ProvisionMemberAccountCommandHandlerTests()
    {
        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _accountProvisioningMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.Available);
        _accountProvisioningMock
            .Setup(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountProvisioningResult>(
                ResultType.Success,
                new AccountProvisioningResult(Guid.NewGuid(), Guid.NewGuid(), "invitation-token")));

        _kurinDataMock.SetupGet(x => x.Memberships).Returns(_membershipsMock.Object);
        _kurinDataMock.SetupGet(x => x.Kurins).Returns(_kurinsMock.Object);
        _membershipsMock
            .Setup(x => x.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _handler = new ProvisionMemberAccountCommandHandler(
            _uowMock.Object,
            _accountProvisioningMock.Object,
            new Mock<IDomainEventPublisher>().Object,
            _emailServiceMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProvisionMemberAccountCommandHandler>.Instance);
    }

    private Member GivenMember(Guid? userKey = null)
    {
        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "Olena",
            LastName = "Invite",
            Email = "olena.invite@example.com",
            PhoneNumber = "123",
            DateOfBirth = new DateOnly(2003, 3, 3),
            UserKey = userKey
        };

        _memberRepoMock.Setup(r => r.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        return member;
    }

    [Fact]
    public async Task Handle_ShouldLinkTheAccount_AndSendTheInvitation()
    {
        var member = GivenMember();

        var result = await _handler.Handle(
            new ProvisionMemberAccountCommand(member.MemberKey),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.UserKey.Should().Be(result.Data!.UserKey);
        result.Data.Sent.Should().BeTrue();
        _accountProvisioningMock.Verify(
            x => x.ProvisionAsync(
                It.Is<AccountProvisioningRequest>(r => r.Email == member.Email),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _emailServiceMock.Verify(
            x => x.SendInvitationEmailAsync(member.Email, "invitation-token", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheLetterCannotBeSent_ShouldKeepTheAccountAndReportTheLetterUnsent()
    {
        var member = GivenMember();
        _emailServiceMock
            .Setup(x => x.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API key is invalid"));

        var result = await _handler.Handle(
            new ProvisionMemberAccountCommand(member.MemberKey),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Sent.Should().BeFalse();
        member.UserKey.Should().Be(result.Data.UserKey);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheMemberAlreadyHasAnAccount_ShouldConflict()
    {
        var member = GivenMember(userKey: Guid.NewGuid());

        var result = await _handler.Handle(
            new ProvisionMemberAccountCommand(member.MemberKey),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _accountProvisioningMock.Verify(
            x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheAddressIsTaken_ShouldConflict()
    {
        var member = GivenMember();
        _accountProvisioningMock
            .Setup(x => x.CheckAvailabilityAsync(member.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.WaitlistPending);

        var result = await _handler.Handle(
            new ProvisionMemberAccountCommand(member.MemberKey),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
    }

    [Fact]
    public async Task Handle_UnknownMember_ShouldReturnNotFound()
    {
        _memberRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member)null!);

        var result = await _handler.Handle(
            new ProvisionMemberAccountCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
    }
}
