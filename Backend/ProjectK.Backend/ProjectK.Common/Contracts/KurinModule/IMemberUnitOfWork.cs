using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// The member module's way to its own table. It is deliberately not part of <see cref="IUnitOfWork"/>:
/// once a repository is reachable from the shared unit of work, every handler in every module can
/// use it, and the boundary exists only in the folder names.
/// <para>
/// The same instance backs both facades for now, so a member write still commits together with
/// whatever else the request is doing. Splitting the transaction is a later step, and one that has
/// to be decided rather than inherited.
/// </para>
/// </summary>
public interface IMemberUnitOfWork
{
    IMemberRepository Members { get; }
    IMemberAwardRepository MemberAwards { get; }
    IMemberWarningRepository MemberWarnings { get; }
    IMemberLifeEntryRepository MemberLifeEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken token = default);
}
