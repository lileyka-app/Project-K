using ProjectK.Common.Contracts.NotificationsModule;
using QRCoder;

namespace ProjectK.Infrastructure.Services.Qr;

public class QrCodeService : IQrCodeService
{
    public string ToPngDataUrl(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
    }
}
