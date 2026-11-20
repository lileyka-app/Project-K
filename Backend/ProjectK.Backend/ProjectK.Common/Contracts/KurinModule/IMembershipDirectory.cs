using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// What the kurin module will tell others about a person's belonging. The counterpart of
/// <c>IMemberDirectory</c>: that one answers "who is this person", this one answers "where have they
/// been", and no other module reads the membership table to do it.
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

    /// <summary>The same for several people in one read, by member; someone with none is absent.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>How many, without reading them.</summary>
    Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurins a person stands in now, each once, the one joined most recently first; read without
    /// the rest of the membership. Where one kurin has to stand for the person, it is the first.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetCurrentKurinKeysForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Everyone who stands in a kurin now, each placed by their membership there, in one read.</summary>
    Task<IReadOnlyCollection<KurinPerson>> GetPeopleInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Some of a kurin's people, as they stand in it: the members, гуртки and accounts the
    /// selection names, in one read. For a page that labels a few people out of the whole kurin.
    /// </summary>
    Task<IReadOnlyCollection<KurinPerson>> GetPeopleInKurinAsync(
        Guid kurinKey,
        MemberSelection selection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A person as the kurin they joined most recently holds them, among those they stand in now —
    /// where one kurin has to stand for the person. One read. Null when they stand nowhere, or do not exist.
    /// </summary>
    Task<KurinPerson?> FindInLatestKurinAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// A person as their youth programme's kurin holds them: the УПЮ kurin they joined most recently
    /// among those they stand in now, or — standing in no УПЮ kurin — the one they joined most recently
    /// at all. Where проби and вмілості are filed, which гурток's probe edition applies and which край's
    /// offer counts. One read. Null when they stand nowhere, or do not exist.
    /// </summary>
    Task<KurinPerson?> FindInYouthKurinAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The same for the person behind an account.</summary>
    Task<KurinPerson?> FindInLatestKurinByAccountAsync(Guid userKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The person behind an account as this kurin holds them: their гурток here, not in the kurin
    /// they joined last. Null when they do not stand in it. For anything asked about one kurin.
    /// </summary>
    Task<KurinPerson?> FindInKurinByAccountAsync(Guid userKey, Guid kurinKey, CancellationToken cancellationToken = default);

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
    /// How many people belong to a kurin now — every current membership, whether or not the
    /// person has an account.
    /// </summary>
    Task<int> CountCurrentInKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for a list of kurins in one read, by kurin. A kurin nobody belongs to is absent
    /// rather than present with zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountCurrentInKurinsAsync(
        IReadOnlyCollection<Guid> kurinKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every membership the kurin has had, current and closed — what the dues module charges from.
    /// </summary>
    Task<IReadOnlyCollection<KurinMembershipRecord>> GetInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurin's people with an account, as pickable entries named by the first office each holds
    /// here. Used where the caller has to choose a person — assigning a mentor, addressing a notice.
    /// The photo stays a blob name for the caller to sign.
    /// </summary>
    Task<IReadOnlyCollection<MemberLookupDto>> GetLookupByKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);
}
