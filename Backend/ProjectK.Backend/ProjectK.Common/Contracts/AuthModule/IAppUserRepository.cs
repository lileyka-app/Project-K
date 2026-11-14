using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// The account queries the business layer needs.
/// <para>
/// These used to be written as LINQ over <c>UserManager.Users</c>, which is an <c>IQueryable</c> —
/// so the handlers had to reference EF Core to await it, and the shape of the query lived in the
/// handler rather than behind the repository line like every other read.
/// </para>
/// </summary>
public interface IAppUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The account holding an address, or <c>null</c>. Used where an onboarding record has to be
    /// tied back to the account it belongs to and only the address is at hand.
    /// </summary>
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many of these accounts finished onboarding. The caller names the accounts because who
    /// belongs to a kurin is a question for membership, and an account record cannot answer it:
    /// <c>AppUser.KurinKey</c> is written once when the account is opened and never again.
    /// </summary>
    Task<int> CountActiveAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the kurin on every account that has stepped into it (<c>ActiveKurinKey</c>). The
    /// column now has a foreign key that sets it null when the kurin goes, but the accounts are
    /// still cleared here, in the same unit of work, so a reader in the same transaction never
    /// sees them scoped to a kurin that no longer exists. Tracked; lands with the caller's
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    Task DetachFromKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurin the account last stepped into (<c>ActiveKurinKey</c>), or <c>null</c> when it never
    /// chose or does not exist. A push that opens a kurin's screen checks it is the kurin the app is in.
    /// </summary>
    Task<Guid?> GetActiveKurinKeyAsync(Guid userKey, CancellationToken cancellationToken = default);
}
