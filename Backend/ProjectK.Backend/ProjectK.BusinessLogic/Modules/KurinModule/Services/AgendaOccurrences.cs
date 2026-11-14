using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The key an answer is stored under for one occurrence of an event: null for a one-off event, the
/// occurrence start for a series. Checked against the same expansion the calendar renders, so an
/// answer cannot be filed under a day the series does not meet on.
/// </summary>
public static class AgendaOccurrences
{
    /// <summary>
    /// Resolves the requested occurrence to its stored key. A one-off event accepts no occurrence
    /// (or its own start); a series requires one that the rule really produces. False means the
    /// request named a day that is not an occurrence of this item. <paramref name="zone"/> is the kurin's.
    /// </summary>
    public static bool TryResolveKey(AgendaItem item, DateTime? requestedStartUtc, TimeZoneInfo zone, out DateTime? key)
    {
        key = null;
        if (item.RecurrenceFrequency == RecurrenceFrequency.None)
        {
            return requestedStartUtc is null || !item.StartUtc.HasValue || SameInstant(requestedStartUtc.Value, item.StartUtc.Value);
        }

        if (requestedStartUtc is null || !item.StartUtc.HasValue)
        {
            return false;
        }

        var wanted = DateTime.SpecifyKind(requestedStartUtc.Value, DateTimeKind.Utc);
        var found = AgendaRecurrence.Expand(item, wanted.AddDays(-1), wanted.AddDays(1), zone)
            .Any(o => SameInstant(o.StartUtc, wanted));
        if (!found)
        {
            return false;
        }

        key = wanted;
        return true;
    }

    /// <summary>The stored key of an occurrence already known to be real (one the expander produced).</summary>
    public static DateTime? KeyOf(AgendaItem item, DateTime occurrenceStartUtc) =>
        item.RecurrenceFrequency == RecurrenceFrequency.None ? null : DateTime.SpecifyKind(occurrenceStartUtc, DateTimeKind.Utc);

    // EF hands back Unspecified, the request binder Utc; the ticks are what matter.
    private static bool SameInstant(DateTime a, DateTime b) => a.Ticks == b.Ticks;
}
