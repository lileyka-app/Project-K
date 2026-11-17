using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>The person behind the token: who they are, their ступені, and where they stand now.</summary>
public sealed record CurrentPerson(
    Guid UserKey,
    MemberSummary Person,
    IReadOnlyList<PlastLevel> Levels,
    IReadOnlyList<MembershipRecord> Memberships)
{
    /// <summary>Whether the youth programme — проби, вмілості, точкування — is theirs in this kurin. The rule the card draws by.</summary>
    public bool HasYouthProgramIn(MembershipRecord membership) =>
        PlastLadderRules.PersonalBranch(Levels, membership.Branch) == KurinBranch.UPYu;

    /// <summary>Whether it is theirs anywhere they stand.</summary>
    public bool HasYouthProgram => Memberships.Any(HasYouthProgramIn);
}
