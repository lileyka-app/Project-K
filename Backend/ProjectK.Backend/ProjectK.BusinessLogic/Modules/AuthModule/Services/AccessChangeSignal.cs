using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using ProjectK.Common.Interfaces.Modules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <inheritdoc />
public sealed class AccessChangeSignal : IAccessChangeSignal
{
    private const int DefaultTokenMinutes = 10;

    // The bearer handler's default skew: a token is accepted this long past its expiry, so a mark
    // has to outlive the token by as much.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _users = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _kurins = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _retention;

    public AccessChangeSignal(TimeProvider time, IConfiguration configuration)
    {
        _time = time;
        var minutes = int.TryParse(configuration["Jwt:ExpiresInMinutes"], out var configured) && configured > 0
            ? configured
            : DefaultTokenMinutes;
        _retention = TimeSpan.FromMinutes(minutes) + ClockSkew;
    }

    public void MarkUser(Guid userKey) => Mark(_users, userKey);

    public void MarkKurin(Guid kurinKey) => Mark(_kurins, kurinKey);

    public bool IsStale(Guid userKey, Guid? kurinKey, DateTimeOffset issuedAt)
    {
        var now = _time.GetUtcNow();
        return Predates(_users, userKey, issuedAt, now)
            || (kurinKey is { } kurin && Predates(_kurins, kurin, issuedAt, now));
    }

    private void Mark(ConcurrentDictionary<Guid, DateTimeOffset> marks, Guid key)
    {
        var now = _time.GetUtcNow();
        marks[key] = now;

        // Marks are rare (an office or a policy saved), so a sweep on each one keeps both maps
        // as small as the changes of the last token lifetime.
        foreach (var (staleKey, at) in _users.Where(entry => now - entry.Value >= _retention))
        {
            _users.TryRemove(new KeyValuePair<Guid, DateTimeOffset>(staleKey, at));
        }

        foreach (var (staleKey, at) in _kurins.Where(entry => now - entry.Value >= _retention))
        {
            _kurins.TryRemove(new KeyValuePair<Guid, DateTimeOffset>(staleKey, at));
        }
    }

    private bool Predates(ConcurrentDictionary<Guid, DateTimeOffset> marks, Guid key, DateTimeOffset issuedAt, DateTimeOffset now)
    {
        if (!marks.TryGetValue(key, out var markedAt) || now - markedAt >= _retention)
        {
            return false;
        }

        // iat is whole seconds, so compare in whole seconds: a token refreshed in the same second
        // as the mark would otherwise read as stale and refresh again until the second passes.
        // A token minted earlier in that same second is missed; it expires on its own.
        return issuedAt.ToUnixTimeSeconds() < markedAt.ToUnixTimeSeconds();
    }
}
