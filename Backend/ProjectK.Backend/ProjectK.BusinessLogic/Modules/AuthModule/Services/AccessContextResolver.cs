using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <inheritdoc />
public sealed class AccessContextResolver : IAccessContextResolver
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IOfficeDirectory _offices;
    private readonly IMembershipDirectory _memberships;
    private readonly IUnitOfWork _unitOfWork;

    public AccessContextResolver(
        UserManager<AppUser> userManager,
        IOfficeDirectory offices,
        IMembershipDirectory memberships,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _offices = offices;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
    }

    public async Task<AccessContext> ResolveAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userKey.ToString());
        return user is null
            ? AccessContext.None(userKey)
            : await ResolveAsync(user, cancellationToken);
    }

    /// <summary>
    /// The same for a user already in hand — the sign-in paths have just loaded one, and looking it
    /// up again by key would be a second round trip for an answer they are holding.
    /// </summary>
    public async Task<AccessContext> ResolveAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SystemRole.Member };

        // The identity store is down to one role that means anything: Admin is granted to a person,
        // not to a person-in-a-kurin, and is the only thing that outlives the kurin they are in.
        if (await _userManager.IsInRoleAsync(user, SystemRole.Admin))
        {
            roles.Add(SystemRole.Admin);
        }

        // The account's choice is a bare key with no foreign key behind it, so it can outlive the
        // kurin: a deleted kurin, or one the seeder rebuilt under a new key. Honouring it then means
        // signing the person in to a kurin that is not there — a scope with nothing in it and, for
        // anyone but an admin, no way to step out (STAB-05). A choice that no longer exists is
        // treated as no choice at all.
        var chosen = user.ResolveScopeKurinKey();
        if (chosen.HasValue && !await _unitOfWork.Kurins.ExistsAsync(chosen.Value, cancellationToken))
        {
            chosen = null;
        }

        var (kurinKey, offices) = chosen.HasValue
            ? (chosen, await _offices.GetForAccountInKurinAsync(user.Id, chosen.Value, cancellationToken))
            : await WhereTheyStandAsync(user.Id, cancellationToken);

        foreach (var office in offices)
        {
            roles.Add(SystemRole.ForOffice(office.Type, office.Role));
        }

        return new AccessContext(user.Id, kurinKey, [.. roles]);
    }

    /// <summary>
    /// Where an account stands when it has never said. The record used to answer this itself
    /// (<c>AppUser.KurinKey</c>); since belonging moved to membership, nothing writes that field any
    /// more, and without this a person who has simply never used the switcher signs in belonging
    /// nowhere — no kurin, and none of the rights their office gives them there.
    /// <para>
    /// With one membership there is nothing to choose. With several, the kurin where they actually
    /// hold an office wins: a виховник of one kurin and a plain member of another is put among the
    /// people they answer for, not wherever they happened to join last. Failing that, the newest —
    /// somewhere real, with the switcher one click away, beats nowhere. An explicit choice is
    /// remembered on the account and never reaches this method again.
    /// </para>
    /// </summary>
    private async Task<(Guid? KurinKey, IReadOnlyCollection<MemberOffice> Offices)> WhereTheyStandAsync(
        Guid userKey,
        CancellationToken cancellationToken)
    {
        var memberships = await _memberships.GetCurrentForAccountAsync(userKey, cancellationToken);
        var newestFirst = memberships
            .OrderByDescending(membership => membership.JoinedAtUtc)
            .ToList();

        if (newestFirst.Count == 0)
        {
            return (null, []);
        }

        foreach (var kurinKey in newestFirst.Select(membership => membership.KurinKey))
        {
            var offices = await _offices.GetForAccountInKurinAsync(userKey, kurinKey, cancellationToken);
            if (offices.Count > 0)
            {
                return (kurinKey, offices);
            }
        }

        return (newestFirst[0].KurinKey, []);
    }
}
