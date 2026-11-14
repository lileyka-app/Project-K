namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>The danger zone as the server holds it.</summary>
/// <param name="Ticket">Sent back in <c>X-Archive-Danger</c> with every deletion; kept in the page's memory only.</param>
/// <param name="ExpiresAtUtc"></param>
/// <param name="DeletionsLeft"></param>
public sealed record ArchiveDangerZoneDto(string Ticket, DateTime ExpiresAtUtc, int DeletionsLeft);
