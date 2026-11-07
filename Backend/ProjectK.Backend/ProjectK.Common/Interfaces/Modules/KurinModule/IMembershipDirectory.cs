using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

/// <summary>
/// What the kurin module will tell others about a person's belonging. The counterpart of
/// <c>IMemberDirectory</c>: that one answers "who is this person", this one answers "where have they
/// been", and neither module reads the other's tables to do it.
/// </summary>
public interface IMembershipDirectory
{
    /// <summary>
    /// Every kurin this person has belonged to, current first and then by when they joined. Past
    /// memberships are included: leaving a kurin does not remove it from a person's history.
    /// </summary>
    Task<IReadOnlyCollection<MembershipRecord>> GetForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default);

    /// <summary>How many, without reading them.</summary>
    /// <summary>The same for several people in one read, by member; someone with none is absent.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurins an account may currently act in. Asked by the access layer, so it is answered from
    /// the account key that memberships carry and never by way of the person behind it.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetKurinKeysForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, named — what an account is shown when it has to choose which kurin to act in.
    /// </summary>
    Task<IReadOnlyCollection<MembershipRecord>> GetCurrentForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for a whole list of accounts, in one read. An account that stands nowhere is absent
    /// from the result rather than present with an empty list.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetCurrentForAccountsAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The accounts currently standing in a kurin. Asked where the subject is the account and not
    /// the person behind it: a beta seat cap counts who can sign in.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetAccountKeysInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every membership the kurin has had, current and closed — what the dues module charges from.
    /// </summary>
    Task<IReadOnlyCollection<KurinMembershipRecord>> GetInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);
}
