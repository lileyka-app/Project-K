using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.Onboarding;

public sealed class ResendInvitationHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IWaitlistRepository> _waitlistEntries = new();
    private readonly Mock<IAppUserRepository> _users = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly ResendInvitationCommandHandler _handler;

    public ResendInvitationHandlerTests()
    {
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.Invitations).Returns(_invitations.Object);
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.WaitlistEntries).Returns(_waitlistEntries.Object);
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.Users).Returns(_users.Object);
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new ResendInvitationCommandHandler(_unitOfWork.Object, _emailService.Object, TimeProvider.System);
    }

    /// <summary>
    /// Holding no live invitation is the ordinary state of someone who needs one resent: theirs
    /// expired, or retention took it. Refusing that case left the panel able to help only the people
    /// who needed no help.
    /// </summary>
    [Fact]
    public async Task IssuesAnInvitationEvenWhenTheEntryHasNoLiveOne()
    {
        var entry = new WaitlistEntry
        {
            WaitlistEntryKey = Guid.NewGuid(),
            Email = "pending@example.com"
        };
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = entry.Email,
            OnboardingStatus = OnboardingStatus.PendingActivation
        };

        _waitlistEntries
            .Setup(repository => repository.GetByKeyAsync(entry.WaitlistEntryKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _invitations
            .Setup(repository => repository.GetActiveByWaitlistEntryKeyAsync(entry.WaitlistEntryKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);
        _users
            .Setup(repository => repository.GetByEmailAsync(entry.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Invitation? issued = null;
        _invitations
            .Setup(repository => repository.Create(It.IsAny<Invitation>(), It.IsAny<CancellationToken>()))
            .Callback<Invitation, CancellationToken>((invitation, _) => issued = invitation);

        var result = await _handler.Handle(new ResendInvitationCommand(entry.WaitlistEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        issued.Should().NotBeNull();
        issued!.TargetUserKey.Should().Be(user.Id);
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            entry.Email,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplacesTheLiveInvitationWhenThereIsOne()
    {
        var entry = new WaitlistEntry
        {
            WaitlistEntryKey = Guid.NewGuid(),
            Email = "pending@example.com"
        };
        var existing = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            WaitlistEntryKey = entry.WaitlistEntryKey,
            TargetUserKey = Guid.NewGuid(),
            Token = "old"
        };

        _waitlistEntries
            .Setup(repository => repository.GetByKeyAsync(entry.WaitlistEntryKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _invitations
            .Setup(repository => repository.GetActiveByWaitlistEntryKeyAsync(entry.WaitlistEntryKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Invitation? issued = null;
        _invitations
            .Setup(repository => repository.Create(It.IsAny<Invitation>(), It.IsAny<CancellationToken>()))
            .Callback<Invitation, CancellationToken>((invitation, _) => issued = invitation);

        var result = await _handler.Handle(new ResendInvitationCommand(entry.WaitlistEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.IsRevoked.Should().BeTrue();
        issued.Should().NotBeNull();
        issued!.TargetUserKey.Should().Be(existing.TargetUserKey);
    }

    [Fact]
    public async Task RefusesAnEntryThatDoesNotExist()
    {
        _waitlistEntries
            .Setup(repository => repository.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);

        var result = await _handler.Handle(new ResendInvitationCommand(Guid.NewGuid()), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("WaitlistEntryNotFound");
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
