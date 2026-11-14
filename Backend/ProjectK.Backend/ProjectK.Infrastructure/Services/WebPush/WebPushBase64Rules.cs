namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// Base64url without padding (RFC 4648 §5), the alphabet every Web Push key and JWT part uses.
/// Decoding accepts padding too: browsers leave it out, but a key pasted into configuration may not.
/// </summary>
internal static class WebPushBase64Rules
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <exception cref="FormatException">The text is not base64url.</exception>
    public static byte[] Decode(string text)
    {
        var standard = text.Trim().TrimEnd('=').Replace('-', '+').Replace('_', '/');
        var padding = (4 - (standard.Length % 4)) % 4;
        return Convert.FromBase64String(standard + new string('=', padding));
    }
}
