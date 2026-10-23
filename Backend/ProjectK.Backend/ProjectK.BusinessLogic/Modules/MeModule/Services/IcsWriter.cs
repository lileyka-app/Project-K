using System.Globalization;
using System.Text;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>
/// One event of the calendar link. Occurrences of a series arrive already expanded, so each one is
/// an event of its own and the file needs neither RRULE nor VTIMEZONE.
/// </summary>
/// <param name="StartUtc">For an all-day event, midnight UTC of its first day.</param>
/// <param name="EndUtc">For an all-day event, its last day (inclusive, as stored); for a timed one, the end instant.</param>
public sealed record IcsEvent(
    string Uid,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    string Summary,
    string? Description,
    string? Location,
    string? Category,
    DateTime StampUtc);

/// <summary>
/// Writes an iCalendar (RFC 5545) file by hand. The format is small and the calendar link needs only
/// a slice of it; a library would be one more package to keep patched for that slice.
/// <para>
/// The traps it handles: lines end in CRLF; TEXT escapes backslash, semicolon, comma and newline;
/// a line longer than 75 octets is folded, and the fold must not split a multi-byte character —
/// Cyrillic is two bytes a letter, so a title of forty letters already needs it.
/// </para>
/// </summary>
public static class IcsWriter
{
    private const int MaxLineOctets = 75;

    public static string Write(string calendarName, string? timeZoneId, IEnumerable<IcsEvent> events)
    {
        var builder = new StringBuilder();
        Line(builder, "BEGIN:VCALENDAR");
        Line(builder, "VERSION:2.0");
        Line(builder, "PRODID:-//Lileyka//Calendar//UK");
        Line(builder, "CALSCALE:GREGORIAN");
        Line(builder, "METHOD:PUBLISH");
        Line(builder, $"X-WR-CALNAME:{Escape(calendarName)}");
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            Line(builder, $"X-WR-TIMEZONE:{Escape(timeZoneId)}");
        }
        // A hint only: Apple and Outlook honour it, Google polls on its own schedule whatever it says.
        Line(builder, "REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        Line(builder, "X-PUBLISHED-TTL:PT1H");

        foreach (var item in events)
        {
            WriteEvent(builder, item);
        }

        Line(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static void WriteEvent(StringBuilder builder, IcsEvent item)
    {
        Line(builder, "BEGIN:VEVENT");
        Line(builder, $"UID:{item.Uid}");
        Line(builder, $"DTSTAMP:{Instant(item.StampUtc)}");
        Line(builder, $"LAST-MODIFIED:{Instant(item.StampUtc)}");

        if (item.IsAllDay)
        {
            // Stored inclusive; iCalendar's DTEND of a date is exclusive.
            var firstDay = item.StartUtc.Date;
            var lastDay = item.EndUtc.Date < firstDay ? firstDay : item.EndUtc.Date;
            Line(builder, $"DTSTART;VALUE=DATE:{Day(firstDay)}");
            Line(builder, $"DTEND;VALUE=DATE:{Day(lastDay.AddDays(1))}");
        }
        else
        {
            var end = item.EndUtc > item.StartUtc ? item.EndUtc : item.StartUtc;
            Line(builder, $"DTSTART:{Instant(item.StartUtc)}");
            Line(builder, $"DTEND:{Instant(end)}");
        }

        Line(builder, $"SUMMARY:{Escape(item.Summary)}");
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            Line(builder, $"DESCRIPTION:{Escape(item.Description)}");
        }
        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            Line(builder, $"LOCATION:{Escape(item.Location)}");
        }
        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            Line(builder, $"CATEGORIES:{Escape(item.Category)}");
        }
        Line(builder, "STATUS:CONFIRMED");
        Line(builder, "TRANSP:OPAQUE");
        Line(builder, "END:VEVENT");
    }

    public static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Instant(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Day(DateTime date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>Appends a content line, folded at 75 octets on a character boundary.</summary>
    public static void Line(StringBuilder builder, string line)
    {
        var octets = 0;
        // The first line holds 75 octets; each continuation spends one on its leading space.
        var limit = MaxLineOctets;

        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                builder.Append("\r\n ");
                octets = 0;
                limit = MaxLineOctets - 1;
            }

            builder.Append(rune.ToString());
            octets += size;
        }

        builder.Append("\r\n");
    }
}
