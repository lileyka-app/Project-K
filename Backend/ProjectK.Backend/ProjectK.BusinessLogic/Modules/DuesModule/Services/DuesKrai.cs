using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// What a kurin's вкладка takes from its край: the currency it is counted in, how its numbers are
/// written, the clock its quarters turn on, and where it is handed up. A quarter turns at midnight
/// in the край's zone, not in UTC, so a kurin in Kyiv is not charged two hours late, nor one in
/// Toronto five hours early.
/// </summary>
public sealed record DuesKrai(string Currency, string NumberLocale, TimeZoneInfo Zone, KraiDuesSettings Dues)
{
    /// <summary>How Lilejka counted before краї had profiles.</summary>
    public static DuesKrai Default { get; } = new("UAH", "uk-UA", AgendaRecurrence.DefaultZone, KraiDuesSettings.Default);

    /// <summary>The day it is in the край when the clock reads <paramref name="now"/>.</summary>
    public DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    /// <summary>The quarter it is in the край when the clock reads <paramref name="now"/>.</summary>
    public DuesQuarter QuarterOf(DateTimeOffset now) => DuesQuarter.Of(Today(now));

    /// <summary>The quarter a moment stored in UTC falls into in the край.</summary>
    public DuesQuarter QuarterOf(DateTime utc) => QuarterOf(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    /// <summary>Whether a new стаття may send its money there. What is already set stays as it is.</summary>
    public bool Takes(DuesRecipient recipient) => recipient switch
    {
        DuesRecipient.Stanytsia => Dues.PaysStanytsia,
        DuesRecipient.Krai => Dues.PaysKrai,
        _ => true
    };

    /// <summary>Whether the kurin's box may hand money on that way.</summary>
    public bool Takes(DuesEntryKind kind) => kind switch
    {
        DuesEntryKind.TransferToStanytsia => Dues.PaysStanytsia,
        DuesEntryKind.TransferToKrai => Dues.PaysKrai,
        _ => true
    };
}

public static class DuesKrais
{
    public static async Task<DuesKrai> DuesKraiForKurinAsync(this IKraiProfileProvider profiles, Guid kurinKey, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetForKurinAsync(kurinKey, cancellationToken);
        return profile is null
            ? DuesKrai.Default
            : new DuesKrai(profile.Locale.Currency, profile.Locale.NumberLocale, AgendaRecurrence.ResolveZone(profile.Locale.TimeZone), profile.Dues);
    }
}
