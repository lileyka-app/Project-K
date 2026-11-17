namespace ProjectK.Common.Contracts.NotificationsModule;

/// <summary>
/// The one place QR codes are drawn: the authenticator link when the second factor is set up, and
/// the phone-app install page on a computer. The browser gets a finished picture and needs no
/// library of its own.
/// </summary>
public interface IQrCodeService
{
    /// <summary>A PNG of the code for <paramref name="text"/>, as a <c>data:</c> URL ready for an <c>img</c>.</summary>
    string ToPngDataUrl(string text);
}
