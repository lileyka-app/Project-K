using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// «Справи» from внески: money waiting on a скарбник — transfers the kurin's one has not acknowledged,
/// and entries in each box, a гурток's or the kurin's own, that nobody has verified.
/// </summary>
public sealed class DuesDutyReader : IDutySource
{
    private readonly IDuesUnitOfWork _dues;
    private readonly IKurinDirectory _kurins;

    public DuesDutyReader(IDuesUnitOfWork dues, IKurinDirectory kurins)
    {
        _dues = dues;
        _kurins = kurins;
    }

    public async Task<IReadOnlyList<PendingDuty>> GetDutiesAsync(DutyViewer viewer, CancellationToken cancellationToken = default)
    {
        var rights = viewer.Rights;
        var keepsKurinBox = rights.WholeKurin(ResourceType.KurinDues, ResourceAction.Update);
        if (!keepsKurinBox && !rights.ForAnyGroup(ResourceType.GroupDues, ResourceAction.Update))
        {
            return [];
        }

        var duties = new List<PendingDuty>();
        var entries = await _dues.DuesEntries.GetForKurinAsync(viewer.KurinKey, cancellationToken);

        if (keepsKurinBox)
        {
            var transfers = entries.Count(e => e.GroupKey.HasValue && e.Kind == DuesEntryKind.TransferToKurin && e.ReceivedAtUtc is null);
            if (transfers > 0)
            {
                duties.Add(new PendingDuty(MyDutyKind.TransfersToConfirm, transfers));
            }

            var own = entries.Count(e => e.GroupKey is null && !e.IsVerified);
            if (own > 0)
            {
                duties.Add(new PendingDuty(MyDutyKind.EntriesToVerify, own));
            }
        }

        var unverified = entries
            .Where(e => e.GroupKey.HasValue && !e.IsVerified)
            .GroupBy(e => e.GroupKey!.Value)
            .Where(g => rights.ForGroup(ResourceType.GroupDues, ResourceAction.Update, g.Key))
            .ToList();
        if (unverified.Count == 0)
        {
            return duties;
        }

        var groupNames = await _kurins.GetGroupNamesAsync(viewer.KurinKey, cancellationToken);
        duties.AddRange(unverified.Select(group => new PendingDuty(
            MyDutyKind.EntriesToVerify,
            group.Count(),
            GroupKey: group.Key,
            GroupName: groupNames.GetValueOrDefault(group.Key, "—"))));
        return duties;
    }
}
