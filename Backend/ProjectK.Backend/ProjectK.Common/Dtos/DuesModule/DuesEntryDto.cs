using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.DuesModule;

public sealed record DuesEntryDto
{
    public required Guid DuesEntryKey { get; init; }
    public required DuesEntryKind Kind { get; init; }
    public required DuesPaymentMethod Method { get; init; }
    public required DuesPaymentMethod? CounterMethod { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly OccurredOn { get; init; }
    public required Guid? MembershipKey { get; init; }
    public required string? MemberName { get; init; }

    public required Guid? CollectedByMemberKey { get; init; }
    public required string? CollectedByName { get; init; }
    public required string? Note { get; init; }
    public required bool IsVerified { get; init; }
    public required DateTime? VerifiedAtUtc { get; init; }
    public required string? VerifiedByName { get; init; }
    public required DateTime? ReceivedAtUtc { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
