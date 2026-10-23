namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// Remembers when what someone may do last changed, so an access token minted before that moment
/// can be told it is stale and the client refreshes at once instead of waiting out the token.
/// <para>
/// Held in memory on one instance: it is a hint that makes a change reach people quickly, not the
/// rule. The server decides every request from the database and the kurin's policy regardless,
/// and a token never outlives its ten minutes.
/// </para>
/// </summary>
public interface IAccessChangeSignal
{
    /// <summary>One account's roles changed: an office, a mentor assignment, a membership, admin.</summary>
    void MarkUser(Guid userKey);

    /// <summary>Everyone in a kurin may now do something else: its «Права й видимість» was saved.</summary>
    void MarkKurin(Guid kurinKey);

    /// <summary>
    /// Whether a token minted at <paramref name="issuedAt"/> for this account and kurin predates a
    /// change marked for either.
    /// </summary>
    bool IsStale(Guid userKey, Guid? kurinKey, DateTimeOffset issuedAt);
}
