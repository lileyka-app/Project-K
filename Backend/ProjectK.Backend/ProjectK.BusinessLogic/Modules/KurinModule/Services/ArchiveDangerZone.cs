using System.Collections.Concurrent;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The danger zones open right now, one per account and kurin, held in the process. The ticket a
/// page carries is only half of it: a zone that is ended here is over at once, whatever the
/// ticket still says, and here is where its deletions are counted down.
/// <para>
/// In memory on purpose: a restart ends every zone, which is the safe way to lose them. The API
/// runs as one instance; a second one would need this in a shared store.
/// </para>
/// </summary>
public sealed class ArchiveDangerZoneRegistry
{
    private readonly ConcurrentDictionary<Guid, ArchiveDangerZoneEntry> _zones = new();

    public void Open(ArchiveDangerZoneEntry entry, DateTime now)
    {
        foreach (var stale in _zones.Where(pair => pair.Value.ExpiresUtc < now.AddHours(-1)
                                                   || (pair.Value.UserKey == entry.UserKey && pair.Value.KurinKey == entry.KurinKey)))
        {
            _zones.TryRemove(stale.Key, out _);
        }

        _zones[entry.TicketId] = entry;
    }

    public ArchiveDangerZoneEntry? Find(Guid ticketId) => _zones.GetValueOrDefault(ticketId);

    /// <summary>The account's zone in the kurin, live or run out, taken off the list.</summary>
    public ArchiveDangerZoneEntry? Close(Guid userKey, Guid kurinKey)
    {
        var found = _zones.FirstOrDefault(pair => pair.Value.UserKey == userKey && pair.Value.KurinKey == kurinKey);
        return found.Value is not null && _zones.TryRemove(found.Key, out var removed) ? removed : null;
    }

    /// <summary>Takes one deletion off a live zone; the deletions left, or null when none were.</summary>
    public int? Spend(Guid ticketId, DateTime now)
    {
        if (!_zones.TryGetValue(ticketId, out var entry))
        {
            return null;
        }

        lock (entry)
        {
            if (entry.ExpiresUtc <= now || entry.DeletionsLeft <= 0)
            {
                return null;
            }

            entry.DeletionsLeft--;
            return entry.DeletionsLeft;
        }
    }
}

public sealed class ArchiveDangerZoneEntry(Guid ticketId, Guid userKey, Guid kurinKey, DateTime expiresUtc, int deletionsLeft)
{
    public Guid TicketId { get; } = ticketId;
    public Guid UserKey { get; } = userKey;
    public Guid KurinKey { get; } = kurinKey;
    public DateTime ExpiresUtc { get; } = expiresUtc;
    public int DeletionsLeft { get; set; } = deletionsLeft;
}

/// <summary>
/// Opens, checks and ends the archive's danger zone. A deletion goes through only with both the
/// right (<c>KurinArchive:Delete</c>) and a live ticket of the caller's own, for the kurin the
/// session acts in, issued under the account's current security stamp.
/// </summary>
public sealed class ArchiveDangerZone
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int Deletions = 10;

    private readonly ArchiveDangerZoneRegistry _registry;
    private readonly IJwtService _jwt;
    private readonly IReauthentication _reauthentication;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public ArchiveDangerZone(
        ArchiveDangerZoneRegistry registry,
        IJwtService jwt,
        IReauthentication reauthentication,
        ICurrentUserContext currentUser,
        TimeProvider time)
    {
        _registry = registry;
        _jwt = jwt;
        _reauthentication = reauthentication;
        _currentUser = currentUser;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>Opens a zone for the caller in the kurin, ending any they had open there.</summary>
    public (string Ticket, ArchiveDangerZoneEntry Entry) Open(Guid userKey, Guid kurinKey, string securityStamp)
    {
        var entry = new ArchiveDangerZoneEntry(Guid.NewGuid(), userKey, kurinKey, Now.Add(Lifetime), Deletions);
        _registry.Open(entry, Now);
        return (_jwt.GenerateArchiveDangerTicket(userKey, kurinKey, entry.TicketId, securityStamp, entry.ExpiresUtc), entry);
    }

    /// <summary>The caller's live zone in this kurin the ticket stands for, or null.</summary>
    public async Task<ArchiveDangerZoneEntry?> CheckAsync(string? ticket, Guid kurinKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticket)
            || _jwt.ReadArchiveDangerTicket(ticket) is not { } read
            || read.UserId != _currentUser.UserId
            || read.KurinKey != kurinKey
            || _currentUser.KurinKey != kurinKey
            || _registry.Find(read.TicketId) is not { } entry
            || entry.UserKey != read.UserId
            || entry.ExpiresUtc <= Now
            || entry.DeletionsLeft <= 0)
        {
            return null;
        }

        return await _reauthentication.GetSecurityStampAsync(read.UserId, cancellationToken) == read.SecurityStamp
            ? entry
            : null;
    }

    public int? Spend(ArchiveDangerZoneEntry entry) => _registry.Spend(entry.TicketId, Now);

    /// <summary>Ends the caller's zone in the kurin; whether it had run out of time, or null when there was none.</summary>
    public bool? Close(Guid userKey, Guid kurinKey) =>
        _registry.Close(userKey, kurinKey) is { } closed ? closed.ExpiresUtc <= Now : null;
}
