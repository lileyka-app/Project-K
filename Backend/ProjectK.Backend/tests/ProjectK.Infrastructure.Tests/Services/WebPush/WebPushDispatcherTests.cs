using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Options.InfrastructureModule;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.Services.WebPush;

namespace ProjectK.Infrastructure.Tests.Services.WebPush;

/// <summary>
/// One notification out to every device of its recipient: what goes over the wire, what the device
/// reads after decrypting, and which devices are forgotten afterwards (404/410 only).
/// </summary>
public class WebPushDispatcherTests
{
    // A throwaway pair for these tests only.
    private const string VapidPublic = "BDeLW-FFPXDIu7DhSQo_OnzzEmrLljJO2-0bszZ5cxFscIcrosM2WTzjOGuZZOCWwujNEObZm0iLGconJxSdIcg";
    private const string VapidPrivate = "9sbYUFXSOOuBssD0Mem-ltekXR7cgcPk7uluQJff8bw";

    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWebPushSubscriptionRepository> _devices = new();
    private readonly Mock<IAppUserRepository> _users = new();
    private readonly FakePushService _pushService = new();
    private readonly List<WebPushSubscription> _registered = [];
    private readonly Dictionary<string, ECDiffieHellman> _deviceKeys = new();
    private readonly Dictionary<string, byte[]> _deviceAuth = new();

    public WebPushDispatcherTests()
    {
        _unitOfWork.SetupGet(x => x.WebPushSubscriptions).Returns(_devices.Object);
        _unitOfWork.SetupGet(x => x.Users).Returns(_users.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _devices
            .Setup(x => x.GetByUserAsync(_userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _registered.ToList());
    }

    private WebPushSender Sender() => new(
        new HttpClient(_pushService),
        Options.Create(new WebPushOptions { PublicKey = VapidPublic, PrivateKey = VapidPrivate, Subject = "mailto:push@example.com" }),
        TimeProvider.System);

    private WebPushDispatcher Dispatcher() => new(_unitOfWork.Object, Sender(), NullLogger<WebPushDispatcher>.Instance);

    private WebPushSubscription Device(string endpoint, HttpStatusCode answer)
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var auth = RandomNumberGenerator.GetBytes(16);
        var device = new WebPushSubscription
        {
            UserKey = _userKey,
            Endpoint = endpoint,
            P256dh = WebPushBase64Rules.Encode(WebPushCryptoRules.ExportPublicKey(key)),
            Auth = WebPushBase64Rules.Encode(auth)
        };
        _registered.Add(device);
        _deviceKeys[endpoint] = key;
        _deviceAuth[endpoint] = auth;
        _pushService.Answers[endpoint] = answer;
        return device;
    }

    private AppNotificationDto Assigned(Guid item) => new()
    {
        NotificationKey = Guid.NewGuid(),
        RecipientUserKey = _userKey,
        Type = AppNotificationType.AgendaItemAssigned,
        Title = "Задачу призначено вам",
        Body = "Принести намет",
        EntityType = "AgendaItem",
        EntityKey = item,
        Route = $"/tasks/{Guid.NewGuid()}",
        DeduplicationKey = $"agenda-assigned:{item}:{_userKey}"
    };

    [Fact]
    public async Task Deliver_ShouldSendToEveryDeviceAndForgetOnlyThoseThePushServiceLost()
    {
        var delivered = Device("https://fcm.googleapis.com/fcm/send/ok", HttpStatusCode.Created);
        var gone = Device("https://fcm.googleapis.com/fcm/send/gone", HttpStatusCode.Gone);
        var unknown = Device("https://web.push.apple.com/unknown", HttpStatusCode.NotFound);
        var throttled = Device("https://updates.push.services.mozilla.com/wpush/v2/busy", HttpStatusCode.TooManyRequests);

        await Dispatcher().DeliverAsync(Assigned(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(4, _pushService.Requests.Count);
        _devices.Verify(x => x.Delete(gone, It.IsAny<CancellationToken>()), Times.Once);
        _devices.Verify(x => x.Delete(unknown, It.IsAny<CancellationToken>()), Times.Once);
        _devices.Verify(x => x.Delete(delivered, It.IsAny<CancellationToken>()), Times.Never);
        _devices.Verify(x => x.Delete(throttled, It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deliver_ShouldSendAnEncryptedSignedRequestTheDeviceCanRead()
    {
        var item = Guid.NewGuid();
        Device("https://fcm.googleapis.com/fcm/send/phone", HttpStatusCode.Created);
        var notification = Assigned(item);

        await Dispatcher().DeliverAsync(notification, CancellationToken.None);

        var request = Assert.Single(_pushService.Requests);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/phone", request.Endpoint);
        Assert.Equal("aes128gcm", request.ContentEncoding);
        Assert.Equal("86400", request.Ttl);
        Assert.StartsWith("vapid t=", request.Authorization, StringComparison.Ordinal);
        Assert.EndsWith($", k={VapidPublic}", request.Authorization, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(DeviceReads(request));
        var shown = json.RootElement.GetProperty("notification");
        Assert.Equal("Задачу призначено вам", shown.GetProperty("title").GetString());
        Assert.Equal("Принести намет", shown.GetProperty("body").GetString());
        Assert.Equal("/m/icons/icon-192.png", shown.GetProperty("icon").GetString());
        Assert.Equal(notification.DeduplicationKey, shown.GetProperty("tag").GetString());
        var data = shown.GetProperty("data");
        Assert.Equal(notification.NotificationKey, data.GetProperty("notificationKey").GetGuid());
        var click = data.GetProperty("onActionClick").GetProperty("default");
        Assert.Equal("navigateLastFocusedOrOpen", click.GetProperty("operation").GetString());
        Assert.Equal($"/m/tabs/tasks/task/{item}", click.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Deliver_ShouldOpenTheInboxForAnotherKurinsReviewQueue()
    {
        Device("https://fcm.googleapis.com/fcm/send/phone", HttpStatusCode.Created);
        _users.Setup(x => x.GetActiveKurinKeyAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var notification = new AppNotificationDto
        {
            NotificationKey = Guid.NewGuid(),
            RecipientUserKey = _userKey,
            Type = AppNotificationType.MemberSkillSubmittedForReview,
            Title = "Вмілість на перевірку",
            Body = "Кухар",
            Route = $"/kurin/{Guid.NewGuid()}/review/skills"
        };

        await Dispatcher().DeliverAsync(notification, CancellationToken.None);

        using var json = JsonDocument.Parse(DeviceReads(Assert.Single(_pushService.Requests)));
        var url = json.RootElement.GetProperty("notification").GetProperty("data")
            .GetProperty("onActionClick").GetProperty("default").GetProperty("url").GetString();
        Assert.Equal(PushRouteRules.Inbox, url);
        Assert.Equal("MemberSkillSubmittedForReview", json.RootElement.GetProperty("notification").GetProperty("tag").GetString());
    }

    [Fact]
    public async Task Deliver_ShouldDoNothingForSomeoneWithoutDevices()
    {
        await Dispatcher().DeliverAsync(Assigned(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(_pushService.Requests);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.GetActiveKurinKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deliver_ShouldForgetADeviceWhoseKeysCannotBeUsed()
    {
        var broken = Device("https://fcm.googleapis.com/fcm/send/broken", HttpStatusCode.Created);
        broken.P256dh = WebPushBase64Rules.Encode(new byte[10]);
        Device("https://fcm.googleapis.com/fcm/send/fine", HttpStatusCode.Created);

        await Dispatcher().DeliverAsync(Assigned(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("https://fcm.googleapis.com/fcm/send/fine", Assert.Single(_pushService.Requests).Endpoint);
        _devices.Verify(x => x.Delete(broken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deliver_ShouldKeepGoingWhenOnePushServiceCannotBeReached()
    {
        var offline = Device("https://fcm.googleapis.com/fcm/send/offline", HttpStatusCode.Created);
        _pushService.Unreachable.Add(offline.Endpoint);
        Device("https://web.push.apple.com/phone", HttpStatusCode.Created);

        await Dispatcher().DeliverAsync(Assigned(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("https://web.push.apple.com/phone", Assert.Single(_pushService.Requests).Endpoint);
        _devices.Verify(x => x.Delete(It.IsAny<WebPushSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deliver_ShouldFailLoudlyOnABrokenServerKeyAndForgetNoDevice()
    {
        Device("https://fcm.googleapis.com/fcm/send/phone", HttpStatusCode.Created);
        var sender = new WebPushSender(
            new HttpClient(_pushService),
            Options.Create(new WebPushOptions { PublicKey = VapidPublic, PrivateKey = "too-short" }),
            TimeProvider.System);
        var dispatcher = new WebPushDispatcher(_unitOfWork.Object, sender, NullLogger<WebPushDispatcher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DeliverAsync(Assigned(Guid.NewGuid()), CancellationToken.None));

        Assert.Empty(_pushService.Requests);
        _devices.Verify(x => x.Delete(It.IsAny<WebPushSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackgroundService_ShouldSendWhatTheRequestQueued()
    {
        Device("https://fcm.googleapis.com/fcm/send/phone", HttpStatusCode.Created);
        var queue = new ChannelPushQueue();
        var services = new ServiceCollection();
        services.AddScoped(_ => _unitOfWork.Object);
        services.AddSingleton(Sender());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<WebPushDispatcher>();
        await using var provider = services.BuildServiceProvider();
        using var worker = new WebPushBackgroundService(queue, provider, NullLogger<WebPushBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        queue.Enqueue(Assigned(Guid.NewGuid()));
        for (var waited = 0; _pushService.Requests.Count == 0 && waited < 100; waited++)
        {
            await Task.Delay(50);
        }

        await worker.StopAsync(CancellationToken.None);

        Assert.Single(_pushService.Requests);
    }

    /// <summary>The device's side: decrypt the body with its private key and auth secret.</summary>
    private byte[] DeviceReads(PushRequest request)
    {
        var body = request.Body;
        var salt = body[..16];
        var serverPublic = body[21..86];
        using var server = WebPushCryptoRules.ImportPublicKey(serverPublic);
        var device = _deviceKeys[request.Endpoint];
        var (contentKey, nonce) = WebPushCryptoRules.DeriveContentKey(
            device.DeriveRawSecretAgreement(server.PublicKey),
            _deviceAuth[request.Endpoint],
            salt,
            WebPushCryptoRules.ExportPublicKey(device),
            serverPublic);

        var record = body[86..];
        var plaintext = new byte[record.Length - 16];
        using var aes = new AesGcm(contentKey, 16);
        aes.Decrypt(nonce, record[..^16], record[^16..], plaintext);
        Assert.Equal((byte)0x02, plaintext[^1]);
        return plaintext[..^1];
    }

    private sealed record PushRequest(string Endpoint, string? ContentEncoding, string? Ttl, string? Authorization, byte[] Body);

    /// <summary>Push services by endpoint: each answers its configured status, or is unreachable.</summary>
    private sealed class FakePushService : HttpMessageHandler
    {
        public Dictionary<string, HttpStatusCode> Answers { get; } = new();
        public HashSet<string> Unreachable { get; } = [];
        public List<PushRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri!.ToString();
            if (Unreachable.Contains(endpoint))
            {
                throw new HttpRequestException("No route to the push service.");
            }

            byte[] body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add(new PushRequest(
                    endpoint,
                    request.Content?.Headers.ContentEncoding.SingleOrDefault(),
                    request.Headers.TryGetValues("TTL", out var ttl) ? ttl.Single() : null,
                    request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : null,
                    body));
            }

            return new HttpResponseMessage(Answers.GetValueOrDefault(endpoint, HttpStatusCode.Created));
        }
    }
}
