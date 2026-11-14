using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ResendInvitation;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Onboarding;

public sealed class ResendInvitationByEmailHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IWaitlistRepository> _waitlistEntries = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly ResendInvitationByEmailCommandHandler _handler;

    public ResendInvitationByEmailHandlerTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManager = new Mock<UserManager<AppUser>>(store.Object, null, null, null, null, null, null, null, null);
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.Invitations).Returns(_invitations.Object);
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.WaitlistEntries).Returns(_waitlistEntries.Object);
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _invitations
            .Setup(repository => repository.GetActiveForTargetUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Invitation>());
        _handler = new ResendInvitationByEmailCommandHandler(
            _userManager.Object,
            _unitOfWork.Object,
            _emailService.Object,
            TimeProvider.System,
            NullLogger<ResendInvitationByEmailCommandHandler>.Instance);
    }

    [Fact]
    public async Task SendsInvitationForPendingActivationUser()
    {
        var user = PendingUser();
        var entry = new WaitlistEntry { WaitlistEntryKey = Guid.NewGuid(), Email = user.Email! };

        _userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _waitlistEntries.Setup(repository => repository.GetByEmailAsync(user.Email!, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        var result = await _handler.Handle(new ResendInvitationByEmailCommand(user.Email!), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            user.Email!,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoesNotSendInvitationForActiveUser()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "active@example.com",
            OnboardingStatus = OnboardingStatus.Active
        };
        _userManager.Setup(manager => manager.FindByEmailAsync(user.Email)).ReturnsAsync(user);

        var result = await _handler.Handle(new ResendInvitationByEmailCommand(user.Email), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotRevealUnknownUser()
    {
        _userManager.Setup(manager => manager.FindByEmailAsync("unknown@example.com"))
            .ReturnsAsync((AppUser?)null);

        var result = await _handler.Handle(
            new ResendInvitationByEmailCommand("unknown@example.com"),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeTrue();
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Retention used to delete the queue entry out from under an account that had not activated
    /// yet, which left the resend with nothing to hang an invitation off — and it answered 202 and
    /// sent nothing, month after month. Having no entry is now something to rebuild, not to give up on.
    /// </summary>
    [Fact]
    public async Task WhenTheQueueEntryIsGone_RebuildsItAndStillSends()
    {
        var user = PendingUser();

        _userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _waitlistEntries
            .Setup(repository => repository.GetByEmailAsync(user.Email!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);

        WaitlistEntry? rebuilt = null;
        _waitlistEntries
            .Setup(repository => repository.Create(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()))
            .Callback<WaitlistEntry, CancellationToken>((entry, _) => rebuilt = entry);

        Invitation? issued = null;
        _invitations
            .Setup(repository => repository.Create(It.IsAny<Invitation>(), It.IsAny<CancellationToken>()))
            .Callback<Invitation, CancellationToken>((invitation, _) => issued = invitation);

        var result = await _handler.Handle(new ResendInvitationByEmailCommand(user.Email!), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        rebuilt.Should().NotBeNull();
        rebuilt!.Email.Should().Be(user.Email);
        rebuilt.VerificationStatus.Should().Be(WaitlistVerificationStatus.ApprovedForInvitation);
        issued.Should().NotBeNull();
        issued!.WaitlistEntryKey.Should().Be(rebuilt.WaitlistEntryKey);
        issued.TargetUserKey.Should().Be(user.Id);
        _emailService.Verify(service => service.SendInvitationEmailAsync(
            user.Email!,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A letter that never left must not cost the person the invitation they already hold, and must
    /// not answer differently from an address nobody has — the status code is the whole leak.
    /// </summary>
    [Fact]
    public async Task WhenTheLetterCannotBeSent_ChangesNothingAndAnswersLikeAnUnknownAddress()
    {
        var user = PendingUser();
        var entry = new WaitlistEntry { WaitlistEntryKey = Guid.NewGuid(), Email = user.Email! };
        var existing = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            WaitlistEntryKey = entry.WaitlistEntryKey,
            Token = "still-good"
        };

        _userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _waitlistEntries
            .Setup(repository => repository.GetByEmailAsync(user.Email!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _invitations
            .Setup(repository => repository.GetActiveForTargetUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });
        _emailService
            .Setup(service => service.SendInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mail is down"));

        var result = await _handler.Handle(new ResendInvitationByEmailCommand(user.Email!), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.IsRevoked.Should().BeFalse();
        _invitations.Verify(repository => repository.Create(It.IsAny<Invitation>(), It.IsAny<CancellationToken>()), Times.Never);
        _waitlistEntries.Verify(repository => repository.Create(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The replacement and the revocations are one write, and the revocations go by account: an
    /// account that collected live tokens across several entries must be left holding exactly one.
    /// </summary>
    [Fact]
    public async Task ReplacingAnInvitation_RevokesEveryLiveOneForTheAccountInTheSameSave()
    {
        var user = PendingUser();
        var entry = new WaitlistEntry { WaitlistEntryKey = Guid.NewGuid(), Email = user.Email! };
        var onThisEntry = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            WaitlistEntryKey = entry.WaitlistEntryKey,
            Token = "old"
        };
        var onAnEntryLongGone = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            WaitlistEntryKey = Guid.NewGuid(),
            Token = "older"
        };

        _userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _waitlistEntries
            .Setup(repository => repository.GetByEmailAsync(user.Email!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _invitations
            .Setup(repository => repository.GetActiveForTargetUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { onThisEntry, onAnEntryLongGone });

        var result = await _handler.Handle(new ResendInvitationByEmailCommand(user.Email!), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        onThisEntry.IsRevoked.Should().BeTrue();
        onAnEntryLongGone.IsRevoked.Should().BeTrue();
        _invitations.Verify(repository => repository.Create(It.IsAny<Invitation>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AppUser PendingUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "pending@example.com",
        FirstName = "Pending",
        LastName = "Person",
        OnboardingStatus = OnboardingStatus.PendingActivation
    };
}
