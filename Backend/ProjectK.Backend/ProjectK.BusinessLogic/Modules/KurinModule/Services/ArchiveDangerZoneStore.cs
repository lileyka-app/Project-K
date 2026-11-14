using System.Collections.Concurrent;

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
public sealed class ArchiveDangerZoneStore
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
