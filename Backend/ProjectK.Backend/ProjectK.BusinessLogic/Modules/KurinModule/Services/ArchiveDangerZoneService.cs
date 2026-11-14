using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Opens, checks and ends the archive's danger zone. A deletion goes through only with both the
/// right (<c>KurinArchive:Delete</c>) and a live ticket of the caller's own, for the kurin the
/// session acts in, issued under the account's current security stamp.
/// </summary>
public sealed class ArchiveDangerZoneService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int Deletions = 10;

    private readonly ArchiveDangerZoneStore _store;
    private readonly IJwtService _jwt;
    private readonly IReauthentication _reauthentication;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public ArchiveDangerZoneService(
        ArchiveDangerZoneStore store,
        IJwtService jwt,
        IReauthentication reauthentication,
        ICurrentUserContext currentUser,
        TimeProvider time)
    {
        _store = store;
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
        _store.Open(entry, Now);
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
            || _store.Find(read.TicketId) is not { } entry
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

    public int? Spend(ArchiveDangerZoneEntry entry) => _store.Spend(entry.TicketId, Now);

    /// <summary>Ends the caller's zone in the kurin; whether it had run out of time, or null when there was none.</summary>
    public bool? Close(Guid userKey, Guid kurinKey) =>
        _store.Close(userKey, kurinKey) is { } closed ? closed.ExpiresUtc <= Now : null;
}
