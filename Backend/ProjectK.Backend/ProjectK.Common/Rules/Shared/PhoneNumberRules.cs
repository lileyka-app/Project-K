using System.Text.RegularExpressions;

namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// The one shape a phone number is stored in: «+», the country code and the number, digits only,
/// as +380501234567 or +420777123456. Spaces, dashes, dots and brackets people type are dropped
/// before the check; anything else that is not a digit makes the number invalid.
/// </summary>
public static partial class PhoneNumberRules
{
    public const int MaxLength = 20;

    public const string ErrorCode = "InvalidPhoneNumber";

    public const string InvalidMessage = "Номер телефону має починатися з «+» і коду країни; разом від 7 до 15 цифр.";

    /// <summary>
    /// The number in the stored shape: <c>""</c> for an empty one, <c>null</c> when it cannot be
    /// one (no «+» and country code, letters, too short or too long).
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var compact = Separators().Replace(raw.Trim(), string.Empty);
        return Stored().IsMatch(compact) ? compact : null;
    }

    public static bool IsValid(string? raw) => Normalize(raw) is not null;

    /// <summary>
    /// <see cref="Normalize"/>, and also the Ukrainian numbers written before the «+» was asked
    /// for: 0501234567, 380501234567, (050) 123-45-67. A roster file carries them that way.
    /// Nine bare digits are left unread: they could be a number of any country.
    /// </summary>
    public static string? NormalizeLegacy(string? raw)
    {
        var normalized = Normalize(raw);
        if (normalized is not null || raw is null)
        {
            return normalized;
        }

        var digits = Separators().Replace(raw.Trim(), string.Empty);
        if (!digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            return Normalize("+" + digits[2..]);
        }

        if (digits.Length == 12 && digits.StartsWith("380", StringComparison.Ordinal))
        {
            return "+" + digits;
        }

        return digits.Length == 10 && digits[0] == '0' ? "+38" + digits : null;
    }

    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex Separators();

    // «+», a country code that does not start with 0, and 7–15 digits in all — E.164 allows no more,
    // and the clients (phone.function.ts, mobile core/phone.ts) hold the same bounds.
    [GeneratedRegex(@"^\+[1-9]\d{6,14}$")]
    private static partial Regex Stored();
}
