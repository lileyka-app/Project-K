using System.Globalization;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// «Справи» from ступені: the senior ones the kurin's people entered for themselves, waiting for the
/// Звʼязковий — the kurin-wide <c>Member:Update</c>, the right that confirms them. One duty per person,
/// leading to their card. The member module does not know the kurin, so the kurin reads its own people
/// and asks for their waiting ступені in one call. Nobody is shown their own: they cannot confirm it.
/// </summary>
public sealed class PlastLevelDutyReader : IDutySource
{
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;

    public PlastLevelDutyReader(IMembershipDirectory memberships, IMemberDirectory members)
    {
        _memberships = memberships;
        _members = members;
    }

    public async Task<IReadOnlyList<PendingDuty>> GetDutiesAsync(DutyViewer viewer, CancellationToken cancellationToken = default)
    {
        if (!viewer.Rights.WholeKurin(ResourceType.Member, ResourceAction.Update))
        {
            return [];
        }

        var people = (await _memberships.GetPeopleInKurinAsync(viewer.KurinKey, cancellationToken))
            .Where(person => person.MemberKey != viewer.Agenda.ViewerMemberKey
                && (person.UserKey is null || person.UserKey != viewer.Agenda.ViewerUserKey))
            .GroupBy(person => person.MemberKey)
            .ToDictionary(group => group.Key, group => group.First());
        if (people.Count == 0)
        {
            return [];
        }

        var waiting = await _members.GetWaitingLevelsAsync(people.Keys.ToList(), cancellationToken);
        return waiting
            .GroupBy(level => level.MemberKey)
            .Where(group => people.ContainsKey(group.Key))
            .Select(group => new PendingDuty(
                MyDutyKind.LevelsToConfirm,
                group.Count(),
                Title: people[group.Key].Person.FullName,
                MemberKey: group.Key))
            .OrderBy(duty => duty.Title, StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true))
            .ThenBy(duty => duty.MemberKey)
            .ToList();
    }
}
