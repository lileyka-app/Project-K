using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IMembershipRepository : IBaseEntityRepository<Membership>
{
    /// <summary>The kurins a person currently belongs to, newest first.</summary>
    Task<IReadOnlyCollection<Membership>> GetActiveForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything a person's belonging amounts to — current and past, with the kurin and гурток
    /// named rather than only keyed. Read for the dossier, which is composed across modules and
    /// cannot join to the kurin's tables to fill the names in.
    /// </summary>
    Task<IReadOnlyCollection<MembershipRecord>> GetRecordsForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetRecordsForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Who used to belong to this kurin and no longer does — one entry per person, their last
    /// spell here.
    /// <para>
    /// Scoped to this kurin's own memberships on purpose: the провід is owed its own history,
    /// which is who was here and when they left, and nothing about where the person went next.
    /// Anyone whose membership here is open again is not former and is absent.
    /// </para>
    /// </summary>
    Task<IReadOnlyCollection<FormerMember>> GetFormerInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>How many memberships a person has held, current and past.</summary>
    Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The kurins an account currently belongs to, by the account key membership carries.</summary>
    Task<IReadOnlyCollection<Guid>> GetKurinKeysForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same kurins, named. Read when an account has to be shown where it may stand, which
    /// takes more than a key: a person recognises their kurin by its number, not its guid.
    /// </summary>
    Task<IReadOnlyCollection<MembershipRecord>> GetCurrentRecordsForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for many accounts at once, keyed by account; an account with no current
    /// membership is absent from the result. Exists so a list of accounts costs one read rather
    /// than one per row.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetCurrentRecordsForAccountsAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The accounts that currently belong to a kurin. Read where the question is about accounts
    /// rather than people — a seat cap counts sign-ins, not names.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetAccountKeysInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>Every membership the kurin has had, current and closed.</summary>
    Task<IReadOnlyCollection<KurinMembershipRecord>> GetRecordsInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the account key onto every current membership of a person. The copy exists so that
    /// authorization never has to read the member record; keeping it correct is this method's job,
    /// and it runs whenever an account is linked.
    /// </summary>
    Task SyncAccountAsync(Guid memberKey, Guid? userKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The person's open membership in one kurin, as a tracked entity, or null when they do not
    /// currently belong to it.
    /// </summary>
    Task<Membership?> GetActiveAsync(
        Guid memberKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a membership as it is given: kind and join date included.
    /// </summary>
    void Open(Membership membership);

    /// <summary>
    /// Puts a person in a kurin and гурток, opening the membership if it is not open yet.
    /// <para>
    /// It never closes a membership in another kurin: a person may stand in several, and leaving
    /// one goes through its own route, with the debt check, the audit entry and the notices.
    /// The write is left on the unit of work, so it commits with whatever caused it.
    /// </summary>
    Task PlaceAsync(
        Guid memberKey,
        Guid? userKey,
        Guid kurinKey,
        Guid? groupKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops every membership these people held, current or past, and answers with the keys of
    /// the memberships dropped. Called when the people themselves are gone: nothing in the database
    /// ties the two tables together, so nobody else would clear these rows — and whoever keys
    /// anything by membership needs to hear which ones went.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> RemoveForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>The same for a kurin being deleted — its whole membership history goes with it.</summary>
    Task RemoveForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets a гурток that is being deleted without touching the membership itself: the person
    /// stays in the kurin, just no longer in that гурток.
    /// </summary>
    Task DetachFromGroupAsync(Guid groupKey, CancellationToken cancellationToken = default);
}
