using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.DuesModule;

/// <summary>A гурток's transfer to the kurin, as the kurin sees it.</summary>
public sealed record DuesTransferDto
{
    public required Guid DuesEntryKey { get; init; }
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required decimal Amount { get; init; }
    public required DuesPaymentMethod Method { get; init; }
    public required DateOnly OccurredOn { get; init; }
    public required string? CollectedByName { get; init; }
    public required string? Note { get; init; }
    public required bool IsReceived { get; init; }
    public required DateTime? ReceivedAtUtc { get; init; }
    public required string? ReceivedByName { get; init; }
}
