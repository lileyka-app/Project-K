using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Resolves agenda creator display names. Most creators are members (resolved cheaply from the member
/// list); creators without a member record in this kurin — e.g. an admin — are looked up through the
/// app user so authorship still shows.
/// </summary>
public static class AgendaCreatorNames
{
    /// <summary>
    /// The members' names by the account they sign in with — the cheap first pass that
    /// <see cref="ResolveAsync"/> fills in. An empty account key is never an author, so it is left out.
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> FromMembers(IEnumerable<MemberSummary> members)
        => MemberSummaryNames.ByAccount(members.Where(member => member.UserKey != Guid.Empty));

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        UserManager<AppUser> userManager,
        IReadOnlyDictionary<Guid, string> fromMembers,
        IEnumerable<AgendaItem> items,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>(fromMembers);

        foreach (var key in AccountsNamedBy(items).Where(key => !names.ContainsKey(key)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(key.ToString());
            if (user is null)
            {
                continue;
            }

            var fullName = PersonNames.FirstLast(user.FirstName, user.LastName);
            names[key] = string.IsNullOrWhiteSpace(fullName) ? (user.Email ?? "—") : fullName;
        }

        return names;
    }

    /// <summary>Authors, and whoever last moved a target or a part, or archived — the board names them all.</summary>
    public static IEnumerable<Guid> AccountsNamedBy(IEnumerable<AgendaItem> items) =>
        items
            .SelectMany(item => item.Assignments
                .SelectMany(a => a.Progress.Select(p => (Guid?)p.ChangedByUserKey).Append(a.StatusChangedByUserKey))
                .Where(key => key.HasValue)
                .Select(key => key!.Value)
                .Append(item.CreatedByUserKey)
                .Concat(item.ArchivedByUserKey is { } archivedBy ? [archivedBy] : []))
            .Where(key => key != Guid.Empty)
            .Distinct();
}
