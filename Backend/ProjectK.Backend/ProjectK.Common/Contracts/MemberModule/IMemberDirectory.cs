using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.MemberModule;

/// <summary>
/// Everything another module may ask of the member module. It is the only way in: no other module
/// holds <c>IMemberRepository</c> or names the <c>Member</c> entity, which is what makes the module
/// liftable later — every call here is one that could be answered over a network instead.
/// <para>
/// Reads that return many people do so in one call by design. A contract that could only answer
/// "one by key" would turn every list into N calls, and N round trips once the module moves out.
/// </para>
/// </summary>
public interface IMemberDirectory
{
    Task<bool> ExistsAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The person a public code names, as a card to confirm against — or null when no code matches.
    /// The match is exact: this is how someone is found when they hand over their code, not a search.
    /// </summary>
    Task<MemberCard?> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>Whether this address already belongs to someone. Guards registration.</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<MemberSummary?> FindAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The person an account belongs to, or null when the account has no member yet.</summary>
    Task<MemberSummary?> FindByAccountAsync(Guid userKey, CancellationToken cancellationToken = default);

    /// <summary>The account a member is linked to. Null when the member has none, or does not exist.</summary>
    Task<Guid?> FindAccountKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The kurin a member belongs to. Null when the member does not exist.</summary>
    Task<Guid?> FindKurinKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Every ступінь recorded for the person, in no particular order; empty for nobody.</summary>
    Task<IReadOnlyCollection<PlastLevel>> GetLevelsAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The person's public code (<c>PL-…</c>); null when the person does not exist.</summary>
    Task<string?> GetPublicIdAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The person's date of birth; null when it was never written down or the person does not exist.</summary>
    Task<DateOnly?> GetDateOfBirthAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>The person's confirmed відзначення, УПЮ and senior alike, newest first.</summary>
    Task<IReadOnlyList<ConfirmedAward>> GetConfirmedAwardsAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Everyone in a kurin, in one read.</summary>
    Task<IReadOnlyCollection<MemberSummary>> GetByKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Some of a kurin's people, as they stand in it: the members, гуртки and accounts the
    /// selection names, in one read. For a page that labels a few people out of the whole kurin.
    /// </summary>
    Task<IReadOnlyCollection<MemberSummary>> GetByKurinAsync(
        Guid kurinKey,
        MemberSelection selection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// These people, in one read, whether or not they still stand in any kurin. For naming someone a
    /// kurin's records still hold after they left it: <see cref="GetByKurinAsync(Guid, CancellationToken)"/>
    /// reads only those who stand in it now.
    /// </summary>
    Task<IReadOnlyCollection<MemberSummary>> GetByKeysAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The перестороги a kurin issued that still stand at <paramref name="nowUtc"/> — not revoked,
    /// not expired. What точкування takes points for.
    /// </summary>
    Task<IReadOnlyCollection<WarningRecord>> GetActiveWarningsInKurinAsync(
        Guid kurinKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// These people as a picker shows them — name, account, photo (signed) and verification — in one
    /// read, by member. Someone who does not exist is absent. The office each holds is the asker's
    /// to say, so <c>UserRole</c> stays empty.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MemberLookupDto>> GetLookupsAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The people a roster's rows could be, in one read: anyone with one of these addresses or phone
    /// numbers, or one of these surnames. Deciding which row is whom is the caller's.
    /// </summary>
    Task<IReadOnlyCollection<MemberIdentity>> FindPossibleMatchesAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> phoneNumbers,
        IReadOnlyCollection<string> lastNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every member in the instance. Exists for the migration preflight report, which has to reason
    /// about the whole table; nothing else should need it.
    /// </summary>
    Task<IReadOnlyCollection<MemberSummary>> GetAllAsync(CancellationToken cancellationToken = default);
}
