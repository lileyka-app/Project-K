using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectK.Infrastructure.Services.WebPush;

namespace ProjectK.Infrastructure.Tests.Services.WebPush;

/// <summary>
/// The payload encryption against RFC 8291's own worked example, a round trip to a device's private
/// key, and the VAPID header's JWT read back and verified with the public key it names.
/// </summary>
public class WebPushCryptoRulesTests
{
    // RFC 8291, Appendix A.
    private const string RfcPlaintext = "When I grow up, I want to be a watermelon";
    private const string RfcServerPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string RfcServerPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string RfcDevicePrivate = "q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94";
    private const string RfcDevicePublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string RfcAuth = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string RfcSalt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string RfcBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Fact]
    public void Encrypt_ShouldReproduceTheRfc8291Example()
    {
        using var serverKey = ImportPrivate(RfcServerPrivate, RfcServerPublic);

        var body = WebPushCryptoRules.Encrypt(
            Encoding.UTF8.GetBytes(RfcPlaintext),
            WebPushBase64Rules.Decode(RfcDevicePublic),
            WebPushBase64Rules.Decode(RfcAuth),
            serverKey,
            WebPushBase64Rules.Decode(RfcSalt));

        Assert.Equal(RfcBody, WebPushBase64Rules.Encode(body));
    }

    [Fact]
    public void Decrypt_ShouldReadTheRfc8291ExampleWithTheDevicesKey()
    {
        using var device = ImportPrivate(RfcDevicePrivate, RfcDevicePublic);

        var plaintext = Decrypt(WebPushBase64Rules.Decode(RfcBody), device, WebPushBase64Rules.Decode(RfcAuth));

        Assert.Equal(RfcPlaintext, Encoding.UTF8.GetString(plaintext));
    }

    [Fact]
    public void Encrypt_ShouldRoundTripToTheDevicesPrivateKey()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var auth = RandomNumberGenerator.GetBytes(16);
        var payload = Encoding.UTF8.GetBytes("{\"notification\":{\"title\":\"Задачу призначено вам\"}}");

        var body = WebPushCryptoRules.Encrypt(
            payload,
            WebPushBase64Rules.Encode(WebPushCryptoRules.ExportPublicKey(device)),
            WebPushBase64Rules.Encode(auth));

        Assert.Equal(payload, Decrypt(body, device, auth));
    }

    [Fact]
    public void Encrypt_ShouldUseAFreshKeyAndSaltForEveryMessage()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p256dh = WebPushBase64Rules.Encode(WebPushCryptoRules.ExportPublicKey(device));
        var auth = RandomNumberGenerator.GetBytes(16);
        var payload = Encoding.UTF8.GetBytes("same");

        var first = WebPushCryptoRules.Encrypt(payload, p256dh, WebPushBase64Rules.Encode(auth));
        var second = WebPushCryptoRules.Encrypt(payload, p256dh, WebPushBase64Rules.Encode(auth));

        Assert.NotEqual(first[..16], second[..16]);
        Assert.NotEqual(first[21..86], second[21..86]);
        Assert.Equal(payload, Decrypt(first, device, auth));
        Assert.Equal(payload, Decrypt(second, device, auth));
    }

    [Fact]
    public void Encrypt_ShouldFillExactlyOneRecordAtMostAndRefuseMore()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p256dh = WebPushBase64Rules.Encode(WebPushCryptoRules.ExportPublicKey(device));
        var auth = WebPushBase64Rules.Encode(RandomNumberGenerator.GetBytes(16));

        var largest = WebPushCryptoRules.Encrypt(new byte[WebPushCryptoRules.MaxPayloadSize], p256dh, auth);

        Assert.Equal(4096, largest.Length);
        Assert.Throws<ArgumentException>(() => WebPushCryptoRules.Encrypt(new byte[WebPushCryptoRules.MaxPayloadSize + 1], p256dh, auth));
    }

    [Fact]
    public void Encrypt_ShouldRefuseAKeyThatIsNotAnUncompressedPoint()
    {
        var auth = WebPushBase64Rules.Encode(RandomNumberGenerator.GetBytes(16));

        Assert.Throws<ArgumentException>(() =>
            WebPushCryptoRules.Encrypt([1, 2, 3], WebPushBase64Rules.Encode(RandomNumberGenerator.GetBytes(33)), auth));
    }

    [Fact]
    public void VapidAuthorization_ShouldCarryAnEs256JwtForThePushServicesOrigin()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = signer.ExportParameters(includePrivateParameters: true);
        var publicKey = WebPushBase64Rules.Encode([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
        var privateKey = WebPushBase64Rules.Encode(parameters.D!);
        var expires = new DateTimeOffset(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

        var header = WebPushCryptoRules.CreateVapidAuthorization(
            new Uri("https://fcm.googleapis.com/fcm/send/abc:def"),
            "mailto:push@example.com",
            publicKey,
            privateKey,
            expires);

        Assert.StartsWith("vapid t=", header, StringComparison.Ordinal);
        var parts = header["vapid t=".Length..].Split(", k=");
        Assert.Equal(2, parts.Length);
        Assert.Equal(publicKey, parts[1]);

        var jwt = parts[0].Split('.');
        Assert.Equal(3, jwt.Length);

        using var head = JsonDocument.Parse(WebPushBase64Rules.Decode(jwt[0]));
        Assert.Equal("ES256", head.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", head.RootElement.GetProperty("typ").GetString());

        using var claims = JsonDocument.Parse(WebPushBase64Rules.Decode(jwt[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(expires.ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal("mailto:push@example.com", claims.RootElement.GetProperty("sub").GetString());

        // JWS ES256: r ‖ s, 32 bytes each, over "header.claims".
        var signature = WebPushBase64Rules.Decode(jwt[2]);
        Assert.Equal(64, signature.Length);
        using var verifier = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = parameters.Q });
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void VapidAuthorization_ShouldKeepANonDefaultPortInTheAudience()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = signer.ExportParameters(includePrivateParameters: true);

        var header = WebPushCryptoRules.CreateVapidAuthorization(
            new Uri("https://push.example.test:8443/send/1"),
            "mailto:push@example.com",
            WebPushBase64Rules.Encode([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]),
            WebPushBase64Rules.Encode(parameters.D!),
            DateTimeOffset.UtcNow.AddHours(1));

        var claims = header["vapid t=".Length..].Split(", k=")[0].Split('.')[1];
        using var json = JsonDocument.Parse(WebPushBase64Rules.Decode(claims));
        Assert.Equal("https://push.example.test:8443", json.RootElement.GetProperty("aud").GetString());
    }

    [Theory]
    [InlineData("BTBZMqHH6r4Tts7J_aSIgg")]
    [InlineData("BTBZMqHH6r4Tts7J_aSIgg==")]
    public void Base64Url_ShouldReadKeysWithOrWithoutPadding(string text)
    {
        var bytes = WebPushBase64Rules.Decode(text);

        Assert.Equal(16, bytes.Length);
        Assert.Equal("BTBZMqHH6r4Tts7J_aSIgg", WebPushBase64Rules.Encode(bytes));
    }

    private static ECDiffieHellman ImportPrivate(string privateKey, string publicKey)
    {
        var point = WebPushBase64Rules.Decode(publicKey);
        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = WebPushBase64Rules.Decode(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..] }
        });
    }

    /// <summary>What the browser does with the body: RFC 8188's header, ECDH with the device's key, AES-GCM.</summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman device, byte[] auth)
    {
        var salt = body[..16];
        Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
        var keyIdLength = body[20];
        var serverPublic = body[21..(21 + keyIdLength)];
        var record = body[(21 + keyIdLength)..];

        using var server = WebPushCryptoRules.ImportPublicKey(serverPublic);
        var secret = device.DeriveRawSecretAgreement(server.PublicKey);
        var (contentKey, nonce) = WebPushCryptoRules.DeriveContentKey(
            secret, auth, salt, WebPushCryptoRules.ExportPublicKey(device), serverPublic);

        var plaintext = new byte[record.Length - 16];
        using (var aes = new AesGcm(contentKey, 16))
        {
            aes.Decrypt(nonce, record[..^16], record[^16..], plaintext);
        }

        // The last record ends with the 0x02 delimiter, after any zero padding.
        var end = Array.FindLastIndex(plaintext, b => b != 0);
        Assert.Equal((byte)0x02, plaintext[end]);
        return plaintext[..end];
    }
}
