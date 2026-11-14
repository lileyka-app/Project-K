using ProjectK.Common.Contracts.KraiModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>The zone a kurin's week is reckoned in: its край's, or the instance's when the kurin is not found.</summary>
public static class KurinTimeZoneRules
{
    public static async Task<TimeZoneInfo> ForKurinAsync(this IKraiProfileDirectory profiles, Guid kurinKey, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetForKurinAsync(kurinKey, cancellationToken);
        return profile is null ? AgendaRecurrenceRules.DefaultZone : AgendaRecurrenceRules.ResolveZone(profile.Locale.TimeZone);
    }
}
