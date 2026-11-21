using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Rules.IdentityModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <summary>
/// Finds an account in a kurin that holds a given seat: an office, or none at all. Shared by the
/// dev role switcher and the public demo, which answer the same question for different reasons.
/// </summary>
public sealed class DemoSeatReader
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;

    public DemoSeatReader(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _members = members;
        _memberships = memberships;
    }

    /// <summary>Somebody who can sign in and holds <paramref name="role"/> in the kurin; never <paramref name="excludeAccountKey"/>.</summary>
    public async Task<AppUser?> FindAsync(DevRole role, Guid kurinKey, Guid? excludeAccountKey, CancellationToken cancellationToken)
    {
        if (role == DevRole.Member)
        {
            return await FindPlainMemberAsync(kurinKey, excludeAccountKey, cancellationToken);
        }

        var office = role switch
        {
            DevRole.Zvyazkovyi => LeadershipRole.Zvyazkovyi,
            DevRole.Vykhovnyk => LeadershipRole.Vykhovnyk,
            DevRole.Kurinnyi => LeadershipRole.Kurinnuy,
            DevRole.Skarbnyk => LeadershipRole.Skarbnyk,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        IEnumerable<Guid> holders = await _unitOfWork.Leaderships.GetActiveOfficeMemberKeysAsync([office], kurinKey, cancellationToken: cancellationToken);
        if (role == DevRole.Vykhovnyk)
        {
            // The holders come in no set order; a виховник without a гурток shows an almost empty demo.
            var leading = (await _unitOfWork.MentorAssignments.GetActiveMentorsInKurinAsync(kurinKey, cancellationToken))
                .Select(mentor => mentor.MemberKey)
                .ToHashSet();
            holders = holders.OrderByDescending(leading.Contains).ThenBy(memberKey => memberKey);
        }

        foreach (var memberKey in holders)
        {
            var accountKey = await _members.FindAccountKeyAsync(memberKey, cancellationToken);
            if (accountKey is null || accountKey == excludeAccountKey)
            {
                continue;
            }

            var user = await _userManager.FindByIdAsync(accountKey.Value.ToString());
            if (user is not null && user.CanSignIn())
            {
                return user;
            }
        }

        return null;
    }

    /// <summary>
    /// Somebody in the kurin with an account, no admin role, and nothing that grants rights beyond
    /// their own гурток: no kurin-wide or КВ office, no mentorship. A гуртковий or писар of a гурток
    /// still counts as a plain youth, and in demo data nearly everyone holds one of those.
    /// </summary>
    private async Task<AppUser?> FindPlainMemberAsync(Guid kurinKey, Guid? excludeAccountKey, CancellationToken cancellationToken)
    {
        var accounts = await _memberships.GetAccountKeysInKurinAsync(kurinKey, cancellationToken);
        foreach (var accountKey in accounts)
        {
            if (accountKey == excludeAccountKey)
            {
                continue;
            }

            var offices = MentorOfficeRules.WithAssignment(
                await _unitOfWork.Leaderships.GetActiveOfficesForAccountInKurinAsync(accountKey, kurinKey, cancellationToken),
                await _unitOfWork.MentorAssignments.HasActiveInKurinAsync(accountKey, kurinKey, cancellationToken));
            if (offices.Any(office => office.Type != LeadershipType.Group))
            {
                continue;
            }

            var user = await _userManager.FindByIdAsync(accountKey.ToString());
            if (user is null || !user.CanSignIn() || await _userManager.IsInRoleAsync(user, SystemRoleNames.Admin))
            {
                continue;
            }

            return user;
        }

        return null;
    }
}
