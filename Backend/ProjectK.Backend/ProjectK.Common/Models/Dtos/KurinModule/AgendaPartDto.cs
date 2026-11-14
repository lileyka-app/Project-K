using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>One person's part of a target done «кожному окремо».</summary>
public sealed record AgendaPartDto
{
    public required Guid MemberKey { get; set; }
    public required string Name { get; set; }
    public required AgendaItemStatus Status { get; set; }
    public required string? ChangedByName { get; set; }
    public required DateTime? ChangedAtUtc { get; set; }
    public required bool CanChangeStatus { get; set; }
}
