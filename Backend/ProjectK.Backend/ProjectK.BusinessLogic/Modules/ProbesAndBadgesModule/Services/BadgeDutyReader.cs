using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>«Справи» from вмілості: those handed in by the youths whose progress the viewer may confirm.</summary>
public sealed class BadgeDutyReader : IDutySource
{
    private readonly IMemberDirectory _members;
    private readonly IMemberProgressDirectory _progress;

    public BadgeDutyReader(IMemberDirectory members, IMemberProgressDirectory progress)
    {
        _members = members;
        _progress = progress;
    }

    public async Task<IReadOnlyList<PendingDuty>> GetDutiesAsync(DutyViewer viewer, CancellationToken cancellationToken = default)
    {
        var rights = viewer.Rights;
        if (!rights.ForAnyGroup(ResourceType.BadgeProgress, ResourceAction.Update))
        {
            return [];
        }

        var people = (await _members.GetByKurinAsync(viewer.KurinKey, cancellationToken))
            .Where(p => rights.ForGroup(ResourceType.BadgeProgress, ResourceAction.Update, p.GroupKey))
            .Select(p => p.MemberKey)
            .ToList();
        var count = await _progress.CountSubmittedBadgesAsync(people, cancellationToken);
        return count > 0 ? [new PendingDuty(MyDutyKind.BadgesToReview, count)] : [];
    }
}
