using ProjectK.Common.Contracts.AuthModule;
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

public class SavedSessionList : ISavedSessionList
{
    /// <summary>Enough for a work account and an admin one with room to spare; keeps the cookie small.</summary>
    public const int MaxAccounts = 5;

    private readonly IRefreshTokenStore _refreshTokens;

    public SavedSessionList(IRefreshTokenStore refreshTokens)
    {
        _refreshTokens = refreshTokens;
    }

    public async Task<IReadOnlyList<SavedSession>> ReadAsync(IEnumerable<string> tokens, CancellationToken cancellationToken)
    {
        var sessions = new List<SavedSession>();
        foreach (var token in tokens.Where(token => !string.IsNullOrWhiteSpace(token)).Distinct(StringComparer.Ordinal))
        {
            var session = await _refreshTokens.FindActiveAsync(token, cancellationToken);
            if (session is not null)
            {
                sessions.Add(new SavedSession(session.Token, session.UserId, session.ExpiresAtUtc));
            }
        }

        return sessions;
    }

    public async Task<IReadOnlyList<SavedSession>> TidyAsync(
        IEnumerable<string> tokens,
        Guid activeUserKey,
        CancellationToken cancellationToken)
    {
        var live = await ReadAsync(tokens, cancellationToken);
        var kept = new List<SavedSession>();
        var accounts = new HashSet<Guid> { activeUserKey };

        // Newest last in the cookie, so walking backwards keeps the latest session of each account.
        foreach (var session in live.Reverse())
        {
            if (kept.Count < MaxAccounts && accounts.Add(session.UserKey))
            {
                kept.Add(session);
                continue;
            }

            await _refreshTokens.RevokeAsync(session.Token, cancellationToken);
        }

        kept.Reverse();
        return kept;
    }
}
