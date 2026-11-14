using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// The two pieces of Web Push that need cryptography, both on what .NET ships: the payload's
/// encryption to one device (RFC 8291, <c>aes128gcm</c> from RFC 8188 — ECDH on P-256, HKDF-SHA-256,
/// AES-128-GCM) and the VAPID header that identifies this server to the push service (RFC 8292, an
/// ES256 JWT). Kept apart from sending so both can be checked against the RFC's own example.
/// </summary>
public static class WebPushCryptoRules
{
    /// <summary>One record carries the whole payload; push services accept at most 4096 bytes.</summary>
    private const int RecordSize = 4096;
    private const int TagSize = 16;
    private const int SaltSize = 16;
    private const int PublicKeySize = 65;

    /// <summary>
    /// The largest payload that still fits one 4096-byte record: the header (86 bytes), the padding
    /// delimiter and the GCM tag come off the top.
    /// </summary>
    public const int MaxPayloadSize = RecordSize - (SaltSize + 4 + 1 + PublicKeySize) - 1 - TagSize;

    private static readonly byte[] KeyInfoLabel = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] ContentKeyInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] NonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>
    /// Encrypts <paramref name="payload"/> to a device: a fresh key pair and salt per message, as the
    /// RFC requires. The result is the request body, sent with <c>Content-Encoding: aes128gcm</c>.
    /// </summary>
    /// <param name="payload">At most <see cref="MaxPayloadSize"/> bytes.</param>
    /// <param name="p256dh">The device's public key, base64url (<c>keys.p256dh</c>).</param>
    /// <param name="auth">The device's auth secret, base64url (<c>keys.auth</c>).</param>
    public static byte[] Encrypt(byte[] payload, string p256dh, string auth)
    {
        using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(payload, WebPushBase64Rules.Decode(p256dh), WebPushBase64Rules.Decode(auth), serverKey, RandomNumberGenerator.GetBytes(SaltSize));
    }

    /// <summary>The same with the server's key pair and the salt given, so a test can replay RFC 8291's example.</summary>
    internal static byte[] Encrypt(byte[] payload, byte[] deviceKey, byte[] authSecret, ECDiffieHellman serverKey, byte[] salt)
    {
        if (payload.Length > MaxPayloadSize)
        {
            throw new ArgumentException($"A push payload is at most {MaxPayloadSize} bytes.", nameof(payload));
        }

        if (authSecret.Length != 16)
        {
            throw new ArgumentException("The auth secret is 16 bytes.", nameof(authSecret));
        }

        if (salt.Length != SaltSize)
        {
            throw new ArgumentException("The salt is 16 bytes.", nameof(salt));
        }

        using var device = ImportPublicKey(deviceKey);
        var serverPublic = ExportPublicKey(serverKey);
        var sharedSecret = serverKey.DeriveRawSecretAgreement(device.PublicKey);

        var (contentKey, nonce) = DeriveContentKey(sharedSecret, authSecret, salt, deviceKey, serverPublic);

        // One record, so it is also the last: the delimiter is 0x02 and no further padding is needed.
        var plaintext = new byte[payload.Length + 1];
        payload.CopyTo(plaintext, 0);
        plaintext[^1] = 0x02;

        var body = new byte[SaltSize + 4 + 1 + PublicKeySize + plaintext.Length + TagSize];
        var header = body.AsSpan(0, SaltSize + 4 + 1 + PublicKeySize);
        salt.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(SaltSize, 4), RecordSize);
        header[SaltSize + 4] = PublicKeySize;
        serverPublic.CopyTo(header[(SaltSize + 5)..]);

        var cipher = body.AsSpan(header.Length, plaintext.Length);
        var tag = body.AsSpan(header.Length + plaintext.Length, TagSize);
        using (var aes = new AesGcm(contentKey, TagSize))
        {
            aes.Encrypt(nonce, plaintext, cipher, tag);
        }

        return body;
    }

    /// <summary>
    /// RFC 8291 §3.3–3.4 and RFC 8188 §2.2: the content key and nonce from the ECDH secret, the
    /// device's auth secret and the message salt. Shared with the tests' decryption.
    /// </summary>
    internal static (byte[] ContentKey, byte[] Nonce) DeriveContentKey(
        byte[] sharedSecret,
        byte[] authSecret,
        byte[] salt,
        byte[] devicePublicKey,
        byte[] serverPublicKey)
    {
        var keyInfo = new byte[KeyInfoLabel.Length + devicePublicKey.Length + serverPublicKey.Length];
        KeyInfoLabel.CopyTo(keyInfo, 0);
        devicePublicKey.CopyTo(keyInfo, KeyInfoLabel.Length);
        serverPublicKey.CopyTo(keyInfo, KeyInfoLabel.Length + devicePublicKey.Length);

        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, ContentKeyInfo);
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, NonceInfo);
        return (contentKey, nonce);
    }

    /// <summary>
    /// The <c>Authorization</c> header value for a push to <paramref name="endpoint"/>:
    /// <c>vapid t=&lt;JWT&gt;, k=&lt;public key&gt;</c>. The JWT's audience is the push service's origin.
    /// </summary>
    public static string CreateVapidAuthorization(
        Uri endpoint,
        string subject,
        string publicKey,
        string privateKey,
        DateTimeOffset expiresAt)
    {
        var publicBytes = WebPushBase64Rules.Decode(publicKey);
        var header = WebPushBase64Rules.Encode(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"));
        var claims = WebPushBase64Rules.Encode(JsonSerializer.SerializeToUtf8Bytes(
            new VapidClaims(endpoint.GetLeftPart(UriPartial.Authority), expiresAt.ToUnixTimeSeconds(), subject)));
        var signingInput = $"{header}.{claims}";

        using var key = ImportSigningKey(publicBytes, WebPushBase64Rules.Decode(privateKey));
        // .NET signs in the IEEE P1363 form (r‖s, 64 bytes), which is what JWS ES256 wants.
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);

        return $"vapid t={signingInput}.{WebPushBase64Rules.Encode(signature)}, k={WebPushBase64Rules.Encode(publicBytes)}";
    }

    internal static ECDiffieHellman ImportPublicKey(byte[] uncompressedPoint)
    {
        var point = ToPoint(uncompressedPoint);
        return ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = point });
    }

    internal static byte[] ExportPublicKey(ECDiffieHellman key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var point = new byte[PublicKeySize];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return point;
    }

    private static ECDsa ImportSigningKey(byte[] publicKey, byte[] privateKey)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("The VAPID private key is 32 bytes.", nameof(privateKey));
        }

        return ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = ToPoint(publicKey),
            D = privateKey
        });
    }

    private static ECPoint ToPoint(byte[] uncompressedPoint)
    {
        if (uncompressedPoint.Length != PublicKeySize || uncompressedPoint[0] != 0x04)
        {
            throw new ArgumentException("A P-256 public key is 65 bytes, uncompressed (0x04 ‖ X ‖ Y).", nameof(uncompressedPoint));
        }

        return new ECPoint { X = uncompressedPoint[1..33], Y = uncompressedPoint[33..] };
    }

    private sealed record VapidClaims(
        [property: JsonPropertyName("aud")] string Audience,
        [property: JsonPropertyName("exp")] long ExpiresAt,
        [property: JsonPropertyName("sub")] string Subject);
}
