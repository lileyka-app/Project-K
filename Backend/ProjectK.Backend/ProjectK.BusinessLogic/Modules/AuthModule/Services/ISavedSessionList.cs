using ProjectK.Common.Records.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The sessions a browser keeps for the accounts it is signed in to but not using, read from and
/// written back to their own httpOnly cookie. Shared by every slice that adds to or takes from it.
/// </summary>
public interface ISavedSessionList
{
    /// <summary>The tokens that still name a live session, in the order given.</summary>
    Task<IReadOnlyList<SavedSession>> ReadAsync(IEnumerable<string> tokens, CancellationToken cancellationToken);

    /// <summary>
    /// The list to write back: one session per account, the newest of each, none for the account now
    /// in use, and at most <see cref="SavedSessionList.MaxAccounts"/>. Every session left out is ended
    /// rather than dropped, so nothing stays alive that no cookie can reach any more.
    /// </summary>
    Task<IReadOnlyList<SavedSession>> TidyAsync(
        IEnumerable<string> tokens,
        Guid activeUserKey,
        CancellationToken cancellationToken);
}
