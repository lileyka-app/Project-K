using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>The person behind the token: who they are, their ступені, and where they stand now.</summary>
/// <param name="UserKey">The account behind the token.</param>
/// <param name="Person">Who they are.</param>
/// <param name="Levels">The ступені that count: one awaiting the Звʼязковий's confirmation is not among them.</param>
/// <param name="Memberships">The memberships they hold now.</param>
/// <param name="BornOn">Their date of birth; null when it was never written down.</param>
public sealed record CurrentPerson(
    Guid UserKey,
    MemberSummary Person,
    IReadOnlyList<PlastLevel> Levels,
    IReadOnlyList<MembershipRecord> Memberships,
    DateOnly? BornOn = null)
{
    /// <summary>Whether the youth programme — проби, вмілості, точкування — is theirs in this kurin. The rule the card draws by.</summary>
    public bool HasYouthProgramIn(MembershipRecord membership) =>
        PlastLadderRules.PersonalBranch(Levels, membership.Branch) == KurinBranch.UPYu;

    /// <summary>Whether it is theirs anywhere they stand.</summary>
    public bool HasYouthProgram => Memberships.Any(HasYouthProgramIn);

    /// <summary>Their улад across the kurins they stand in (<see cref="UladRules"/>).</summary>
    public KurinBranch Ulad => UladRules.Of(Levels, Memberships.Select(m => m.Branch));

    /// <summary>Whether they are an adult on <paramref name="today"/> (<see cref="AdulthoodRules"/>).</summary>
    public bool IsAdultOn(DateOnly today) => AdulthoodRules.IsAdult(BornOn, today);
}
