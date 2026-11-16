using System.Text.RegularExpressions;

namespace ProjectK.Infrastructure.Logging.TelegramDevAlerts;

/// <summary>Emails and secrets never leave in an alert, whatever field they hid in.</summary>
public static class RedactionRules
{
    // A message that takes longer than this to redact is dropped rather than sent half-redacted.
    private static readonly TimeSpan RedactTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RedactTimeout);

    private static readonly Regex TokenRegex = new(
        @"(?i)(bearer\s+)[a-z0-9._~+/=-]+|(?i)(token|password|secret|apikey|api_key|code)=([^\s;&<]+)",
        RegexOptions.Compiled,
        RedactTimeout);

    public static string Apply(string value)
    {
        var redacted = EmailRegex.Replace(value, "[email]");
        return TokenRegex.Replace(redacted, match =>
        {
            if (match.Groups[1].Success)
            {
                return $"{match.Groups[1].Value}[redacted]";
            }

            if (match.Groups[2].Success)
            {
                return $"{match.Groups[2].Value}=[redacted]";
            }

            return "[redacted]";
        });
    }
}
