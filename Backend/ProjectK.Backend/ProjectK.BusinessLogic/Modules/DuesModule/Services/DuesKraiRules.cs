using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

public static class DuesKraiRules
{
    public static async Task<DuesKrai> DuesKraiForKurinAsync(this IKraiProfileDirectory profiles, Guid kurinKey, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetForKurinAsync(kurinKey, cancellationToken);
        return profile is null ? DuesKrai.Default : profile.ToDuesKrai();
    }

    public static DuesKrai ToDuesKrai(this KraiProfile profile) =>
        new(profile.Locale.Currency, profile.Locale.NumberLocale, AgendaRecurrenceRules.ResolveZone(profile.Locale.TimeZone), profile.Dues);
}
