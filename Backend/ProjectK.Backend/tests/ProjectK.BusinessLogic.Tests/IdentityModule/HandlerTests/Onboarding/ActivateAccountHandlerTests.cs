using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.IdentityModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.Onboarding;

/// <summary>
/// The path a new account walks exactly once. It is also the one place where getting authorization
/// wrong locks a kurin's first leader out of their own kurin, because the office is seated while the
/// activating caller is still anonymous.
/// </summary>
public class ActivateAccountHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IWaitlistRepository> _waitlistEntries = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IMemberActivationWriter> _activation = new();
    private readonly Mock<ILoginResponseService> _loginResponses = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly ActivateAccountCommandHandler _handler;

    public ActivateAccountHandlerTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManager = new Mock<UserManager<AppUser>>(store.Object, null, null, null, null, null, null, null, null);

        _unitOfWork.SetupGet(x => x.Invitations).Returns(_invitations.Object);
        _unitOfWork.SetupGet(x => x.WaitlistEntries).Returns(_waitlistEntries.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _userManager.Setup(x => x.AddPasswordAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _loginResponses
            .Setup(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) => Logins.Empty with { UserKey = user.Id, Email = user.Email! });

        // The member side answers the activation by opening a person; read back, it is this one.
        _memberDirectory
            .Setup(x => x.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userKey, CancellationToken _) => new MemberSummary(Guid.NewGuid(), userKey, Guid.Empty, null, "Ліна", "Лідер", "leader@example.com", null));

        _handler = new ActivateAccountCommandHandler(_unitOfWork.Object, _memberDirectory.Object, _userManager.Object, _activation.Object, _clock, _loginResponses.Object, _events.Object);
    }

    private Invitation GivenInvitation(Guid userKey, DateTime? expiresAtUtc = null)
    {
        var invitation = new Invitation
        {
            Token = "token",
            TargetUserKey = userKey,
            WaitlistEntryKey = Guid.NewGuid(),
            ExpiresAtUtc = expiresAtUtc ?? Now.UtcDateTime.AddDays(1)
        };

        _invitations.Setup(x => x.GetByTokenAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        return invitation;
    }

    private AppUser GivenUser(Guid userKey)
    {
        var user = new AppUser { Id = userKey, Email = "leader@example.com" };
        _userManager.Setup(x => x.FindByIdAsync(userKey.ToString())).ReturnsAsync(user);
        return user;
    }

    /// <summary>The queue entry; for a founder, also the kurin their approval opened.</summary>
    private void GivenWaitlistEntry(Invitation invitation, bool isKurinLeaderCandidate, Guid? foundedKurinKey = null)
    {
        _waitlistEntries
            .Setup(x => x.GetByKeyAsync(invitation.WaitlistEntryKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitlistEntry
            {
                Email = "leader@example.com",
                IsKurinLeaderCandidate = isKurinLeaderCandidate,
                FoundedKurinKey = foundedKurinKey
            });
    }

    [Fact]
    public async Task RefusesAnExpiredInvitation()
    {
        GivenInvitation(Guid.NewGuid(), expiresAtUtc: Now.UtcDateTime.AddMinutes(-1));

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("InvalidInvitationToken");
        _userManager.Verify(x => x.AddPasswordAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefusesAnUnknownToken()
    {
        _invitations
            .Setup(x => x.GetByTokenAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("InvalidInvitationToken");
    }

    [Fact]
    public async Task RefusesWhenTheInvitationHasNoUser()
    {
        var invitation = GivenInvitation(Guid.NewGuid());
        invitation.TargetUserKey = null;

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("InvitationHasNoUser");
    }

    [Fact]
    public async Task ReportsWhyThePasswordWasRejected()
    {
        var userKey = Guid.NewGuid();
        var invitation = GivenInvitation(userKey);
        GivenUser(userKey);
        GivenWaitlistEntry(invitation, isKurinLeaderCandidate: false);
        _userManager
            .Setup(x => x.AddPasswordAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too short" }));

        var result = await _handler.Handle(new ActivateAccountCommand("token", "short"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("PasswordNotSet");
        result.ErrorMessage.Should().Contain("Password too short");
    }

    [Fact]
    public async Task ActivatesAnOrdinaryMemberWithoutSeatingAnOffice()
    {
        var userKey = Guid.NewGuid();
        var invitation = GivenInvitation(userKey);
        GivenUser(userKey);
        GivenWaitlistEntry(invitation, isKurinLeaderCandidate: false);
        _members
            .Setup(x => x.GetByEmailAsync("leader@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _userManager.Verify(x => x.AddToRoleAsync(It.IsAny<AppUser>(), SystemRoleNames.Member), Times.Once);
        _activation.Verify(x => x.SeatFounderAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        invitation.UsedAtUtc.Should().NotBeNull();
        _events.Verify(x => x.PublishAsync(It.Is<AccountActivated>(e => e.Account.UserKey == userKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A founder lands in the kurin their approval opened, and a kurin-leader candidate is seated in
    /// it too, both as the member the activation opened and read back after the save.
    /// </summary>
    [Fact]
    public async Task TakesTheFounderIntoTheirKurinAndSeatsThem()
    {
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var invitation = GivenInvitation(userKey);
        GivenUser(userKey);
        GivenWaitlistEntry(invitation, isKurinLeaderCandidate: true, foundedKurinKey: kurinKey);
        _memberDirectory
            .Setup(x => x.FindByAccountAsync(userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(memberKey, userKey, Guid.Empty, null, "Ліна", "Лідер", "leader@example.com", null));

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _activation.Verify(x => x.JoinFoundedKurinAsync(memberKey, kurinKey, It.IsAny<CancellationToken>()), Times.Once);
        _activation.Verify(x => x.SeatFounderAsync(memberKey, kurinKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakesAFounderWhoIsNoLeaderCandidateInWithoutSeatingThem()
    {
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var invitation = GivenInvitation(userKey);
        GivenUser(userKey);
        GivenWaitlistEntry(invitation, isKurinLeaderCandidate: false, foundedKurinKey: kurinKey);

        var result = await _handler.Handle(new ActivateAccountCommand("token", "Password@1"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _activation.Verify(x => x.JoinFoundedKurinAsync(It.IsAny<Guid>(), kurinKey, It.IsAny<CancellationToken>()), Times.Once);
        _activation.Verify(x => x.SeatFounderAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
