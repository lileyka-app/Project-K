using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.NotificationsModule.Features.PhoneApp;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.NotificationsModule;

namespace ProjectK.BusinessLogic.Tests.NotificationsModule.PhoneApp;

public class GetPhoneAppInstallCodeQueryHandlerTests
{
    /// <summary>Echoes what it was asked to encode, so the test can see the address inside the code.</summary>
    private sealed class EchoQrCodes : IQrCodeService
    {
        public string ToPngDataUrl(string text) => $"qr:{text}";
    }

    private sealed class CountingQrCodes : IQrCodeService
    {
        public int Drawn { get; private set; }

        public string ToPngDataUrl(string text)
        {
            Drawn++;
            return $"qr:{text}";
        }
    }

    [Fact]
    public async Task Handle_ShouldDrawTheCodeOnce_AndServeItAfterwards()
    {
        // A host of its own: the drawn codes live for the process, so other tests must not share it.
        var site = Options.Create(new EmailOptions { BaseUrl = $"https://{Guid.NewGuid():N}.example" });
        var qrCodes = new CountingQrCodes();

        var first = await new GetPhoneAppInstallCodeQueryHandler(qrCodes, site).Handle(new GetPhoneAppInstallCodeQuery(), CancellationToken.None);
        var second = await new GetPhoneAppInstallCodeQueryHandler(qrCodes, site).Handle(new GetPhoneAppInstallCodeQuery(), CancellationToken.None);

        Assert.Equal(1, qrCodes.Drawn);
        Assert.Equal(first.Data!.QrCode, second.Data!.QrCode);
    }

    [Theory]
    [InlineData("https://lileyka.example")]
    [InlineData("https://lileyka.example/")]
    public async Task Handle_ShouldPointAtTheInstallPage_OnTheSitesPublicAddress(string baseUrl)
    {
        var handler = new GetPhoneAppInstallCodeQueryHandler(
            new EchoQrCodes(),
            Options.Create(new EmailOptions { BaseUrl = baseUrl }));

        var result = await handler.Handle(new GetPhoneAppInstallCodeQuery(), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("https://lileyka.example/m/install", result.Data!.Url);
        Assert.Equal("qr:https://lileyka.example/m/install", result.Data.QrCode);
    }
}
