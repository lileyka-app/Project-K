using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>Navigation targets for agenda notifications, matched to the frontend routes.</summary>
public static class AgendaRoutes
{
    public static string For(AgendaItem item) =>
        item.Kind == AgendaItemKind.Task ? $"/tasks/{item.KurinKey}" : $"/calendar/{item.KurinKey}";
}

/// <summary>
/// Fans an item's assignments out to the app-user keys that should be notified. A member with no
/// linked user account is skipped (per the notification-events policy), and the actor never notifies
/// themselves.
/// </summary>
public static class AgendaNotificationRecipients
{
    public static async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        IUnitOfWork uow,
        IMemberDirectory directory,
        AgendaItem item,
        Guid actorUserKey,
        CancellationToken cancellationToken)
    {
        // This runs on every agenda write: only the people the targets reach are read, and the
        // whole kurin only when the kurin itself is a target.
        var members = await AgendaPeople.ReadAsync(directory, item.KurinKey, AgendaPeople.ToNotify(item), cancellationToken);
        var recipients = new HashSet<Guid>();

        foreach (var assignment in item.Assignments)
        {
            switch (assignment.TargetType)
            {
                case AgendaTargetType.Kurin:
                    foreach (var member in members)
                    {
                        AddIfLinked(recipients, member.UserKey);
                    }
                    break;

                case AgendaTargetType.Group:
                    foreach (var member in members.Where(m => m.GroupKey == assignment.TargetKey))
                    {
                        AddIfLinked(recipients, member.UserKey);
                    }
                    break;

                case AgendaTargetType.Member:
                    var target = members.FirstOrDefault(m => m.MemberKey == assignment.TargetKey);
                    AddIfLinked(recipients, target?.UserKey);
                    break;

                case AgendaTargetType.Tag:
                    var carriers = await uow.KurinTags.GetMemberKeysAsync(assignment.TargetKey, cancellationToken);
                    IReadOnlyCollection<MemberSummary> carrierAccounts = carriers.Count == 0
                        ? []
                        : await directory.GetByKurinAsync(item.KurinKey, new MemberSelection(carriers.ToList(), [], []), cancellationToken);
                    foreach (var carrier in carrierAccounts)
                    {
                        AddIfLinked(recipients, carrier.UserKey);
                    }
                    break;

                case AgendaTargetType.Leadership:
                    var officeUserKeys = await uow.Leaderships
                        .GetActiveMemberUserKeysForLeadershipAsync(assignment.TargetKey, cancellationToken);
                    foreach (var userKey in officeUserKeys)
                    {
                        AddIfLinked(recipients, userKey);
                    }
                    break;
            }
        }

        recipients.Remove(actorUserKey);
        return recipients;
    }

    private static void AddIfLinked(HashSet<Guid> recipients, Guid? userKey)
    {
        if (userKey.HasValue && userKey.Value != Guid.Empty)
        {
            recipients.Add(userKey.Value);
        }
    }
}
