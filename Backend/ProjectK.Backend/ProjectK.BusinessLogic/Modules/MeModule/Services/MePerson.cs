using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>The person behind the token: who they are, their ступені, and where they stand now.</summary>
public sealed record MePersonContext(
    Guid UserKey,
    MemberSummary Person,
    IReadOnlyList<PlastLevel> Levels,
    IReadOnlyList<MembershipRecord> Memberships)
{
    /// <summary>Whether the youth programme — проби, вмілості, точкування — is theirs in this kurin. The rule the card draws by.</summary>
    public bool HasYouthProgramIn(MembershipRecord membership) =>
        PlastLadder.PersonalBranch(Levels, membership.Branch) == KurinBranch.UPYu;

    /// <summary>Whether it is theirs anywhere they stand.</summary>
    public bool HasYouthProgram => Memberships.Any(HasYouthProgramIn);
}

/// <summary>Reads the person once for whatever the dashboard asks; null when no person stands behind the account.</summary>
public sealed class MePerson
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;

    public MePerson(ICurrentUserContext currentUser, IMembershipDirectory memberships, IMemberDirectory members)
    {
        _currentUser = currentUser;
        _memberships = memberships;
        _members = members;
    }

    public async Task<MePersonContext?> ReadAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return null;
        }

        var person = await _members.FindByAccountAsync(userKey, cancellationToken);
        if (person is null)
        {
            return null;
        }

        var levels = (await _members.GetLevelsAsync(person.MemberKey, cancellationToken)).ToList();
        var memberships = (await _memberships.GetCurrentForAccountAsync(userKey, cancellationToken))
            .OrderBy(m => m.KurinNumber)
            .ToList();

        return new MePersonContext(userKey, person, levels, memberships);
    }
}
