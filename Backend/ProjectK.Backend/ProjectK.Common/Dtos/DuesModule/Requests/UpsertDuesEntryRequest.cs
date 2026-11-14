using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>One operation of a гурток's box, new or edited.</summary>
public sealed class UpsertDuesEntryRequest
{
    public DuesEntryKind Kind { get; set; }
    public DuesPaymentMethod Method { get; set; }
    public DuesPaymentMethod? CounterMethod { get; set; }
    public decimal Amount { get; set; }
    public DateOnly OccurredOn { get; set; }
    public Guid? MembershipKey { get; set; }
    public Guid? CollectedByMemberKey { get; set; }
    public string? Note { get; set; }
}
