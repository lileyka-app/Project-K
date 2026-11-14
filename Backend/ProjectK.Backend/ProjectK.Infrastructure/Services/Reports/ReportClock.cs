using System.Globalization;

namespace ProjectK.Infrastructure.Services.Reports;

/// <summary>
/// Turns the stored UTC timestamps into the hours the reader's own clock showed.
/// <para>
/// The server keeps and reasons in UTC and has no idea where the reader is, so the zone travels
/// with the request — the browser is the only party that knows it. Split out of the renderer
/// because the two rules worth getting wrong are here: an id that came off the wire must never
/// throw, and a <see cref="DateTimeKind.Unspecified"/> value must be read as UTC.
/// </para>
/// </summary>
public static class ReportClock
{
    /// <summary>
    /// The zone behind an IANA id, or UTC when it is missing or unknown. .NET reads IANA ids on
    /// every platform this runs on, but the id is whatever a client sent: a typo, a stale zone
    /// name, or junk must degrade to UTC rather than fail the whole report.
    /// </summary>
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
    }

    /// <summary>The zone's own name, or "UTC" — so a reader knows whose clock the hours belong to.</summary>
    public static string Label(TimeZoneInfo zone)
        => zone == TimeZoneInfo.Utc ? "UTC" : zone.Id;

    /// <summary>
    /// The moment as that zone reads it, or "-" when there is none.
    /// <para>
    /// The kind is forced to UTC first. Everything is stored as UTC, but EF hands <c>datetime2</c>
    /// back as <see cref="DateTimeKind.Unspecified"/>, and converting that leans on whatever zone
    /// the server happens to be in — which in a container is UTC, and on a developer's machine is
    /// not. That is the kind of difference that only shows up in production.
    /// </para>
    /// </summary>
    public static string Format(DateTime? value, TimeZoneInfo zone)
    {
        if (value is not DateTime moment)
        {
            return "-";
        }

        var utc = DateTime.SpecifyKind(moment, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
