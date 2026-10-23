using System.Security.Cryptography;
using System.Text;

namespace ProjectK.Common.Security;

/// <summary>
/// One-way digest of a high-entropy secret (a random token, not a password), so the row can be found
/// by it without keeping the secret itself.
/// </summary>
public static class SecretHash
{
    public static string Sha256Hex(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>A fresh random token, URL-safe base64 without padding.</summary>
    public static string NewToken(int bytes = 32)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
