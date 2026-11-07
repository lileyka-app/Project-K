namespace ProjectK.Common.Models.Records;

/// <summary>
/// The archive's danger-zone ticket read back: whose it is, the kurin it was issued for, its own
/// id (what the server keeps live and ends early) and the account's stamp at issue.
/// </summary>
public sealed record ArchiveDangerTicket(Guid UserId, Guid KurinKey, Guid TicketId, string SecurityStamp);
