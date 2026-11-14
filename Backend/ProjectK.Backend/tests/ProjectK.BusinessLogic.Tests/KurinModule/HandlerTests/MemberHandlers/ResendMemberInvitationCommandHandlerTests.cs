using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

/// <summary>A letter that went to a mistyped address is replaced by one sent to the address on the record.</summary>
public class ResendMemberInvitationCommandHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IAccountProvisioningService> _accountsMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly ResendMemberInvitationCommandHandler _handler;

    public ResendMemberInvitationCommandHandlerTests()
    {
        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new ResendMemberInvitationCommandHandler(
            _uowMock.Object,
            _accountsMock.Object,
            _emailServiceMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResendMemberInvitationCommandHandler>.Instance);
    }

    private Member GivenMember(Guid? userKey)
    {
        var member = new Member { MemberKey = Guid.NewGuid(), Email = "fixed@example.com", UserKey = userKey };
        _memberRepoMock.Setup(r => r.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        return member;
    }

    [Fact]
    public async Task Handle_PendingAccount_ShouldSendTheNewTokenToTheRecordedAddress()
    {
        var member = GivenMember(Guid.NewGuid());
        _accountsMock
            .Setup(a => a.ReissueInvitationAsync(member.UserKey!.Value, "fixed@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountProvisioningResult>(
                ResultType.Success,
                new AccountProvisioningResult(member.UserKey!.Value, Guid.NewGuid(), "new-token")));

        var result = await _handler.Handle(new ResendMemberInvitationCommand(member.MemberKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _emailServiceMock.Verify(e => e.SendInvitationEmailAsync("fixed@example.com", "new-token", It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheLetterCannotBeSent_ShouldSayNotSentAndChangeNoInvitation()
    {
        var member = GivenMember(Guid.NewGuid());
        _accountsMock
            .Setup(a => a.ReissueInvitationAsync(member.UserKey!.Value, "fixed@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountProvisioningResult>(
                ResultType.Success,
                new AccountProvisioningResult(member.UserKey!.Value, Guid.NewGuid(), "new-token")));
        _emailServiceMock
            .Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API key is invalid"));

        var result = await _handler.Handle(new ResendMemberInvitationCommand(member.MemberKey), CancellationToken.None);

        result.ErrorCode.Should().Be(MemberErrors.InvitationNotSent);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActivatedAccount_ShouldRefuseAndSendNothing()
    {
        var member = GivenMember(Guid.NewGuid());
        _accountsMock
            .Setup(a => a.ReissueInvitationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AccountProvisioningResult>.Failure(ResultType.Conflict, "AccountNotPending", "active"));

        var result = await _handler.Handle(new ResendMemberInvitationCommand(member.MemberKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("AccountNotPending");
        _emailServiceMock.VerifyNoOtherCalls();
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MemberWithoutAccount_ShouldConflict()
    {
        var member = GivenMember(null);

        var result = await _handler.Handle(new ResendMemberInvitationCommand(member.MemberKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("NoAccount");
    }
}
