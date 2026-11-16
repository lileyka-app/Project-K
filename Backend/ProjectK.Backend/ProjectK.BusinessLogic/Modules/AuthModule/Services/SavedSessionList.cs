using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

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
