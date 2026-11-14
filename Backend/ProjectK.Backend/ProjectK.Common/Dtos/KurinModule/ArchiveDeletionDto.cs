namespace ProjectK.Common.Dtos.KurinModule;

/// <param name="DeletionsLeft">What the ticket still allows; at zero the zone has ended.</param>
public sealed record ArchiveDeletionDto(int DeletionsLeft);
