using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// One operation in a cash box: a гурток's (<see cref="GroupKey"/> set) or the kurin's own (null).
/// <para>
/// Once a впорядник marks it verified it is locked: it is not edited or deleted until the mark is
/// taken off, and a mistake in a verified entry is put right with a <see cref="DuesEntryKind.Correction"/>.
/// Nothing is ever hard-deleted — money needs a trail, kept in <see cref="Events"/>.
/// </para>
/// </summary>
public class DuesEntry : Entity
{
    public Guid DuesEntryKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid? GroupKey { get; set; }

    /// <summary>Whose вкладка this is, for kinds that concern a person.</summary>
    public Guid? MembershipKey { get; set; }

    public DuesEntryKind Kind { get; set; }
    public DuesPaymentMethod Method { get; set; }

    /// <summary>Where the money went, for <see cref="DuesEntryKind.Exchange"/> only.</summary>
    public DuesPaymentMethod? CounterMethod { get; set; }

    /// <summary>Positive, except a <see cref="DuesEntryKind.Correction"/>, which may lower a balance.</summary>
    public decimal Amount { get; set; }

    public DateOnly OccurredOn { get; set; }

    /// <summary>Who took the money in — a person of this kurin.</summary>
    public Guid? CollectedByMemberKey { get; set; }

    public string? Note { get; set; }

    public Guid? CreatedByUserKey { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }
    public Guid? VerifiedByUserKey { get; set; }

    /// <summary>For a transfer to the kurin: when the курінний скарбник confirmed it arrived.</summary>
    public DateTime? ReceivedAtUtc { get; set; }
    public Guid? ReceivedByUserKey { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserKey { get; set; }

    public ICollection<DuesEntryEvent> Events { get; set; } = [];

    public bool IsVerified => VerifiedAtUtc.HasValue;
    public bool IsDeleted => DeletedAtUtc.HasValue;
}
