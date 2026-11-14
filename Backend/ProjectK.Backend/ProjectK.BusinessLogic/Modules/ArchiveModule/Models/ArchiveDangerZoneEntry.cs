namespace ProjectK.BusinessLogic.Modules.ArchiveModule.Models;

public sealed class ArchiveDangerZoneEntry(Guid ticketId, Guid userKey, Guid kurinKey, DateTime expiresUtc, int deletionsLeft)
{
    public Guid TicketId { get; } = ticketId;
    public Guid UserKey { get; } = userKey;
    public Guid KurinKey { get; } = kurinKey;
    public DateTime ExpiresUtc { get; } = expiresUtc;
    public int DeletionsLeft { get; set; } = deletionsLeft;
}
