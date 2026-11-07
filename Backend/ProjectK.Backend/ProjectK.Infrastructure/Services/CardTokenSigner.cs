using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

namespace ProjectK.Infrastructure.Services;

/// <summary>
/// Signs «перевірити картку» tokens with HMAC-SHA256 under a key derived from the JWT key, so there
/// is no second secret to manage, and no JWT could ever be read as a card token or the other way.
/// A token is the member key, a 32-bit expiry in Unix seconds and 12 bytes of the MAC: 32 bytes,
/// 43 characters of base64url — small enough for a QR that reads from across a table.
/// </summary>
public sealed class CardTokenSigner : ICardTokenSigner
{
    private const int MacLength = 12;
    private const int PayloadLength = 16 + 4;
    private readonly byte[] _key;

    public CardTokenSigner(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not set.");
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtKey), Encoding.UTF8.GetBytes("lileyka:card-token:v1"));
    }

    public string Sign(Guid memberKey, DateTime expiresAtUtc)
    {
        var token = new byte[PayloadLength + MacLength];
        memberKey.TryWriteBytes(token.AsSpan(0, 16));
        BinaryPrimitives.WriteUInt32BigEndian(token.AsSpan(16, 4), (uint)new DateTimeOffset(expiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds());
        HMACSHA256.HashData(_key, token.AsSpan(0, PayloadLength)).AsSpan(0, MacLength).CopyTo(token.AsSpan(PayloadLength));
        return Base64Url.EncodeToString(token);
    }

    public Guid? Read(string token, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length != PayloadLength + MacLength)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, bytes.AsSpan(0, PayloadLength)).AsSpan(0, MacLength);
        if (!CryptographicOperations.FixedTimeEquals(expected, bytes.AsSpan(PayloadLength)))
        {
            return null;
        }

        var expires = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4))).UtcDateTime;
        return expires > nowUtc ? new Guid(bytes.AsSpan(0, 16)) : null;
    }
}
