using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Contracts.MemberModule;

/// <summary>
/// The person behind the request's token — the backend's one answer to "who am I". The person is
/// read once per request and kept for the rest of it, so every check, signature and journal entry of
/// one request sees the same person. A key that names somebody else is a question for
/// <see cref="IMemberDirectory.FindByAccountAsync"/>, not for this.
/// </summary>
public interface ICurrentMember
{
    /// <summary>The account behind the token; null when nobody is signed in.</summary>
    Guid? UserKey { get; }

    /// <summary>The person the account belongs to; null when nobody is signed in or no person stands behind the account.</summary>
    Task<MemberSummary?> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>Their member key. Null when no person stands behind the account — an administrator without a card.</summary>
    Task<Guid?> MemberKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>The person with their ступені and the memberships they hold now, for whatever the dashboard asks; null as above.</summary>
    Task<CurrentPerson?> ReadPersonAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The actor's key, the name a person will read in the trail, and the office that authorised
    /// the action. Worked out once: every signature, trail row and journal entry of one request names
    /// the same person the same way.
    /// </summary>
    Task<AuditActor> ActorAsync(CancellationToken cancellationToken = default);
}
