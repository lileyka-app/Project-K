using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// One thing that happened to a dues entry, and what the entry looked like afterwards. Kept for as
/// long as the entry: unlike progress audit, money history is not cleaned up.
/// </summary>
public class DuesEntryEvent : Entity
{
    /// <summary>
    /// Left for EF to generate: an event is added through its entry's collection, and a child that
    /// arrives with a key already set is taken for an existing row and updated, not inserted.
    /// </summary>
    public Guid DuesEntryEventKey { get; set; }
    public Guid DuesEntryKey { get; set; }

    /// <summary><c>Created</c>, <c>Updated</c>, <c>Deleted</c>, <c>Verified</c>, <c>Unverified</c>, <c>Received</c>, <c>Unreceived</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public Guid? ActorUserKey { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The entry as it stood after the change, as JSON.</summary>
    public string Snapshot { get; set; } = string.Empty;

    public DuesEntry DuesEntry { get; set; } = null!;
}
