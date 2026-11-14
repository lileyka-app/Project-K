using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>The one decision the «Проба» tile makes: which проба to show.</summary>
public static class GrowthSummaryRules
{
    /// <summary>
    /// The проба in progress; else one completed and waiting to be verified; else the first in the
    /// catalogue's order not yet verified; null once every проба is done.
    /// </summary>
    public static string? CurrentProbeId(IEnumerable<string> catalogOrder, IEnumerable<(string ProbeId, ProbeProgressStatus Status)> progresses)
    {
        var status = progresses.ToDictionary(p => p.ProbeId, p => p.Status);
        var order = catalogOrder.ToList();

        return order.FirstOrDefault(id => status.GetValueOrDefault(id) == ProbeProgressStatus.InProgress)
            ?? order.FirstOrDefault(id => status.GetValueOrDefault(id) == ProbeProgressStatus.Completed)
            ?? order.FirstOrDefault(id => status.GetValueOrDefault(id) != ProbeProgressStatus.Verified);
    }
}
