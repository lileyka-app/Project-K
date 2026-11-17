using System.Security.Cryptography;
using System.Text;

namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// Compares a caller-supplied secret with the configured one in constant time, so the answer takes
/// as long for a wrong first byte as for a wrong last one. Used wherever a header or body field is
/// checked against a shared key: the rate-limit bypass, the load-test login, the e2e reset token.
/// </summary>
public static class SecretComparisonRules
{
    public static bool Matches(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
