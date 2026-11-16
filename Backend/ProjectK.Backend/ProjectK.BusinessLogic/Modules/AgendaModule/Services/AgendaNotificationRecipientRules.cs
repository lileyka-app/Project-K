using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>
/// Fans an item's assignments out to the app-user keys that should be notified. A member with no
/// linked user account is skipped (per the notification-events policy), and the actor never notifies
/// themselves.
/// </summary>
public static class AgendaNotificationRecipientRules
{
    public static async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        IUnitOfWork uow,
        IMemberDirectory directory,
        IOfficeDirectory offices,
        AgendaItem item,
        Guid actorUserKey,
        CancellationToken cancellationToken)
    {
        // This runs on every agenda write: only the people the targets reach are read, and the
        // whole kurin only when the kurin itself is a target.
        var members = await AgendaPeopleRules.ReadAsync(directory, item.KurinKey, AgendaPeopleRules.ToNotify(item), cancellationToken);
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
                    var officeUserKeys = await offices.GetHolderAccountKeysAsync(assignment.TargetKey, cancellationToken);
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
