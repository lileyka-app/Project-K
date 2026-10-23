using System.Globalization;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;

/// <summary>
/// How a duty is named for hiding, and when a hidden one stays hidden. The key is the same on every
/// read of the same duty; the fingerprint is its size. A duty that counts things stays hidden while
/// the count has not grown — one handled while hidden does not bring it back, one more does.
/// </summary>
public static class DutyDismissals
{
    /// <summary>«EventWithoutAttendance:{kurin}:-:{event}:20261006T160000Z» — kind, kurin, гурток, event, occurrence; «-» for none.</summary>
    public static string KeyOf(MyDutyDto duty) => string.Join(':',
        duty.Kind.ToString(),
        duty.Kurin.KurinKey.ToString("N"),
        duty.GroupKey?.ToString("N") ?? "-",
        duty.AgendaItemKey?.ToString("N") ?? "-",
        duty.OccurrenceStartUtc?.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) ?? "-");

    public static string FingerprintOf(MyDutyDto duty) => duty.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a key names a duty at all: the kinds this read writes, and nothing longer than the column.</summary>
    public static bool IsKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > DismissedDuty.DutyKeyMaxLength)
        {
            return false;
        }

        var parts = key.Split(':');
        return parts.Length == 5 && Enum.GetNames<MyDutyKind>().Contains(parts[0], StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="duty"/> is still the one that was hidden: no bigger than it was then.</summary>
    public static bool StillHidden(MyDutyDto duty, DismissedDuty dismissal)
    {
        var now = FingerprintOf(duty);
        return int.TryParse(now, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            && int.TryParse(dismissal.Fingerprint, NumberStyles.Integer, CultureInfo.InvariantCulture, out var then)
            ? count <= then
            : now == dismissal.Fingerprint;
    }
}
