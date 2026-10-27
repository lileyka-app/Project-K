using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.InviteKurinFounder;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Onboarding;

public class InviteKurinFounderHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IAccountProvisioningService> _provisioning = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ICurrentUserContext> _admin = new();
    private readonly Mock<IBackendCache> _cache = new();
    private readonly Guid _adminKey = Guid.NewGuid();
    private readonly Guid _accountKey = Guid.NewGuid();
    private readonly InviteKurinFounderCommandHandler _handler;
    private Kurin? _openedKurin;
    private WaitlistEntry? _entry;
    private AccountProvisioningRequest? _provisioned;

    public InviteKurinFounderHandlerTests()
    {
        _unitOfWork.Setup(x => x.WaitlistEntries).Returns(_waitlist.Object);
        _unitOfWork.Setup(x => x.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(x => x.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _admin.Setup(x => x.UserId).Returns(_adminKey);
        _krais.Setup(x => x.ExistsAsync(It.IsIn(Krai.Ukraine, Krai.Poland), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _kurins
            .Setup(x => x.Create(It.IsAny<Kurin>(), It.IsAny<CancellationToken>()))
            .Callback<Kurin, CancellationToken>((kurin, _) => _openedKurin = kurin);
        _waitlist
            .Setup(x => x.Create(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()))
            .Callback<WaitlistEntry, CancellationToken>((entry, _) => _entry = entry);
        _provisioning
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.Available);
        _provisioning
            .Setup(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AccountProvisioningRequest, CancellationToken>((request, _) => _provisioned = request)
            .ReturnsAsync(new ServiceResult<AccountProvisioningResult>(
                ResultType.Success, new AccountProvisioningResult(_accountKey, Guid.NewGuid(), "token")));

        _handler = new InviteKurinFounderCommandHandler(
            _unitOfWork.Object,
            _provisioning.Object,
            _email.Object,
            _admin.Object,
            TimeProvider.System,
            Options.Create(new KraiOptions()),
            _cache.Object,
            NullLogger<InviteKurinFounderCommandHandler>.Instance);
    }

    private static InviteKurinFounderCommand Command(int? number = 47, string? krai = null, string email = " founder@example.com ") =>
        new(email, " Ореста ", "Коваль", "+380 50 123 45 67", number, krai);

    [Fact]
    public async Task Handle_OpensTheKurin_AndAnApprovedFounderEntryThatRemembersIt()
    {
        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        _openedKurin!.Number.Should().Be(47);
        _openedKurin.KraiCode.Should().Be(Krai.Ukraine);
        _entry!.FoundedKurinKey.Should().Be(_openedKurin.KurinKey);
        _entry.IsKurinLeaderCandidate.Should().BeTrue();
        _entry.IsPreparatoryKurin.Should().BeFalse();
        _entry.VerificationStatus.Should().Be(WaitlistVerificationStatus.ApprovedForInvitation);
        _entry.Source.Should().Be(WaitlistEntrySource.DirectAdd);
        _entry.ReviewedByUserKey.Should().Be(_adminKey);
        _entry.Email.Should().Be("founder@example.com");
        _entry.FirstName.Should().Be("Ореста");
        _entry.PhoneNumber.Should().Be("+380501234567");
        result.Data.Should().Be(new KurinFounderInvitation(_openedKurin.KurinKey, _accountKey, "founder@example.com", InvitationSent: true));
    }

    [Fact]
    public async Task Handle_HangsTheAccountOffThatEntry_AndSendsTheInvitation()
    {
        await _handler.Handle(Command(), CancellationToken.None);

        _provisioned!.WaitlistEntryKey.Should().Be(_entry!.WaitlistEntryKey);
        _provisioned.Email.Should().Be("founder@example.com");
        _email.Verify(x => x.SendInvitationEmailAsync("founder@example.com", "token", It.IsAny<CancellationToken>()), Times.Once);
        // The admin's cached list of kurins must show the one just opened.
        _cache.Verify(x => x.Invalidate(BackendCachePolicies.KurinReads), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutANumber_OpensAPreparatoryKurin()
    {
        await _handler.Handle(Command(number: null), CancellationToken.None);

        _openedKurin!.Number.Should().BeNull();
        _entry!.IsPreparatoryKurin.Should().BeTrue();
        _kurins.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ChecksTheNumberInTheChosenKrai()
    {
        // Ukraine's kurin 47 does not stand in the way of Poland's.
        _kurins.Setup(x => x.ExistsAsync(Krai.Ukraine, 47, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(Command(krai: " PL "), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        _openedKurin!.KraiCode.Should().Be(Krai.Poland);
        _entry!.KraiCode.Should().Be(Krai.Poland);
    }

    [Fact]
    public async Task Handle_RefusesATakenNumber_BeforeOpeningAnything()
    {
        _kurins.Setup(x => x.ExistsAsync(Krai.Ukraine, 47, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinNumberTaken");
        _openedKurin.Should().BeNull();
        _provisioning.Verify(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RefusesAnUnknownKrai()
    {
        var result = await _handler.Handle(Command(krai: "zz"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("KraiNotFound");
        _openedKurin.Should().BeNull();
    }

    [Theory]
    [InlineData(AccountAvailability.EmailTaken, "EmailTaken")]
    [InlineData(AccountAvailability.WaitlistPending, "WaitlistPending")]
    public async Task Handle_RefusesAnAddressThatIsAlreadyHeld(AccountAvailability availability, string errorCode)
    {
        _provisioning
            .Setup(x => x.CheckAvailabilityAsync("founder@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(availability);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be(errorCode);
        _openedKurin.Should().BeNull();
        _provisioning.Verify(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PassesOnARefusedAccount_WithoutSaving()
    {
        _provisioning
            .Setup(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AccountProvisioningResult>.Failure(ResultType.BadRequest, "UserNotCreated", "Invalid email."));

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("UserNotCreated");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _email.Verify(x => x.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReportsALetterThatDidNotGo_InsteadOfFailing()
    {
        _email
            .Setup(x => x.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.InvitationSent.Should().BeFalse();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
