namespace ProjectK.Common.Dtos.NotificationsModule;

/// <summary>
/// What a computer shows for «Встановити на телефон»: the phone app's install page, and a QR code
/// of it for the phone's camera.
/// </summary>
public class PhoneAppInstallCodeDto
{
    public string Url { get; set; } = string.Empty;

    /// <summary>A PNG <c>data:</c> URL of the code for <see cref="Url"/>.</summary>
    public string QrCode { get; set; } = string.Empty;
}
