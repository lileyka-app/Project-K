using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.InfrastructureModule;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

/// <summary>
/// A device asking for pushes: registered once per endpoint, refreshed on every app start, moved to
/// whoever signs in on it, and forgotten only by its own account.
/// </summary>
public class PushSubscriptionHandlersTests
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-1";

    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWebPushSubscriptionRepository> _devices = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();

    private static readonly IOptions<WebPushOptions> On = Options.Create(new WebPushOptions
    {
        PublicKey = "BDeLW-FFPXDIu7DhSQo_OnzzEmrLljJO2-0bszZ5cxFscIcrosM2WTzjOGuZZOCWwujNEObZm0iLGconJxSdIcg",
        PrivateKey = "test-only"
    });

    private static readonly IOptions<WebPushOptions> Off = Options.Create(new WebPushOptions());

    public PushSubscriptionHandlersTests()
    {
        _unitOfWork.SetupGet(x => x.WebPushSubscriptions).Returns(_devices.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _currentUser.SetupGet(x => x.UserId).Returns(_userKey);
    }

    [Fact]
    public async Task PublicKey_ShouldBeTheConfiguredOneOrNull()
    {
        var on = await new GetPushPublicKeyQueryHandler(On).Handle(new GetPushPublicKeyQuery(), CancellationToken.None);
        var off = await new GetPushPublicKeyQueryHandler(Off).Handle(new GetPushPublicKeyQuery(), CancellationToken.None);

        on.Data!.PublicKey.Should().Be(On.Value.PublicKey);
        off.Type.Should().Be(ResultType.Success);
        off.Data!.PublicKey.Should().BeNull();
    }

    [Fact]
    public async Task Subscribe_ShouldRegisterANewDevice()
    {
        WebPushSubscription? created = null;
        _devices
            .Setup(x => x.Create(It.IsAny<WebPushSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<WebPushSubscription, CancellationToken>((device, _) => created = device);

        var result = await Subscribe(new SubscribeToPushCommand(Endpoint, "p256dh", "auth", "Mozilla/5.0 (iPhone)"));

        result.Type.Should().Be(ResultType.Success);
        created.Should().NotBeNull();
        created!.UserKey.Should().Be(_userKey);
        created.Endpoint.Should().Be(Endpoint);
        created.P256dh.Should().Be("p256dh");
        created.Auth.Should().Be("auth");
        created.UserAgent.Should().Be("Mozilla/5.0 (iPhone)");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Subscribe_ShouldRefreshTheSameEndpointAndMoveItToWhoeverAsked()
    {
        var existing = new WebPushSubscription
        {
            UserKey = Guid.NewGuid(),
            Endpoint = Endpoint,
            P256dh = "old",
            Auth = "old",
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        _devices.Setup(x => x.GetByEndpointAsync(Endpoint, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Subscribe(new SubscribeToPushCommand(Endpoint, "new-key", "new-auth", null));

        existing.UserKey.Should().Be(_userKey);
        existing.P256dh.Should().Be("new-key");
        existing.Auth.Should().Be("new-auth");
        existing.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _devices.Verify(x => x.Create(It.IsAny<WebPushSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _devices.Verify(x => x.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Subscribe_ShouldBeRefusedWhenThisServerSendsNoPushes()
    {
        var handler = new SubscribeToPushCommandHandler(_unitOfWork.Object, _currentUser.Object, Off);

        var result = await handler.Handle(new SubscribeToPushCommand(Endpoint, "p", "a", null), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("PushNotConfigured");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unsubscribe_ShouldForgetOnlyTheCallersOwnDevice()
    {
        var mine = new WebPushSubscription { UserKey = _userKey, Endpoint = Endpoint };
        var theirs = new WebPushSubscription { UserKey = Guid.NewGuid(), Endpoint = Endpoint + "-other" };
        _devices.Setup(x => x.GetByEndpointAsync(mine.Endpoint, It.IsAny<CancellationToken>())).ReturnsAsync(mine);
        _devices.Setup(x => x.GetByEndpointAsync(theirs.Endpoint, It.IsAny<CancellationToken>())).ReturnsAsync(theirs);
        var handler = new UnsubscribeFromPushCommandHandler(_unitOfWork.Object, _currentUser.Object);

        var own = await handler.Handle(new UnsubscribeFromPushCommand(mine.Endpoint), CancellationToken.None);
        var other = await handler.Handle(new UnsubscribeFromPushCommand(theirs.Endpoint), CancellationToken.None);
        var unknown = await handler.Handle(new UnsubscribeFromPushCommand("https://fcm.googleapis.com/fcm/send/none"), CancellationToken.None);

        own.Data.Should().BeTrue();
        other.Type.Should().Be(ResultType.Success);
        other.Data.Should().BeFalse();
        unknown.Type.Should().Be(ResultType.Success);
        _devices.Verify(x => x.Delete(mine, It.IsAny<CancellationToken>()), Times.Once);
        _devices.Verify(x => x.Delete(theirs, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAA", true)]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://evil.example.com/fcm.googleapis.com", false)]
    [InlineData("https://fcm.googleapis.com.evil.example.com/x", false)]
    [InlineData("https://127.0.0.1/push", false)]
    [InlineData("not a url", false)]
    public void Validator_ShouldAcceptOnlyRealPushServices(string endpoint, bool valid)
    {
        var result = new SubscribeToPushCommandValidator().Validate(new SubscribeToPushCommand(endpoint, "p256dh", "auth", null));

        result.IsValid.Should().Be(valid);
    }

    private Task<ProjectK.Common.Records.Shared.ServiceResult<MediatR.Unit>> Subscribe(SubscribeToPushCommand command) =>
        new SubscribeToPushCommandHandler(_unitOfWork.Object, _currentUser.Object, On).Handle(command, CancellationToken.None);
}
