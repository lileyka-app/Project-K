using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// What the kurin module will tell others about who holds which office: the проводи of a kurin, who
/// sits in them, and what a person or an account holds. Offices are answered for one kurin at a time,
/// which is what keeps a виховник's rights from following them into another.
/// <para>
/// The access layer asks by account and is answered by the account's own key, never by the person
/// behind it: that read goes through memberships and offices and never touches the member table. The
/// reads that name a person do so by member key, and hand back keys rather than people.
/// </para>
/// </summary>
public interface IOfficeDirectory
{
    /// <summary>
    /// The offices this account currently holds inside one kurin. An office of another kurin, or one
    /// held under a membership that has since ended, is not among them. Running a гурток of the
    /// kurin as виховник counts as the КВ's Виховник (<c>MentorOfficeRules</c>).
    /// </summary>
    Task<IReadOnlyCollection<MemberOffice>> GetForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every office a person has held, in any kurin, current and ended, as their card shows it: the
    /// role, its провід's type, the гурток it was held in and that гурток's kurin.
    /// </summary>
    Task<IReadOnlyList<LeadershipHistoryDto>> GetHeldByMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default);

    /// <summary>The проводи and the КВ a person currently sits in, by their keys.</summary>
    Task<IReadOnlyList<Guid>> GetLeadershipKeysForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default);

    /// <summary>Every провід and КВ of a kurin: the курінний and the КВ by the kurin, a гуртковий by its гурток.</summary>
    Task<IReadOnlyList<LeadershipRef>> GetLeadershipsInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The member keys of those who currently hold an office in each of these проводи, in one call; a
    /// провід nobody holds now answers with an empty list.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetHolderMemberKeysAsync(
        IReadOnlyCollection<Guid> leadershipKeys,
        CancellationToken cancellationToken = default);

    /// <summary>The accounts of those who currently hold an office in this провід.</summary>
    Task<IReadOnlyList<Guid>> GetHolderAccountKeysAsync(
        Guid leadershipKey,
        CancellationToken cancellationToken = default);
}
