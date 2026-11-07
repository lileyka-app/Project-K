using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// A kurin asking the kurin a person stands in to let them go, or to let them stand in both.
/// <para>
/// Nobody moves between kurins on their own, and no kurin takes someone from another without asking:
/// the Звʼязковий of the asking kurin (<see cref="ToKurinKey"/>) starts it, the Звʼязковий of the
/// person's kurin (<see cref="FromKurinKey"/>) approves or declines, and the person is told. A request
/// nobody answers lapses at <see cref="ExpiresAtUtc"/>.
/// </para>
/// </summary>
public class MembershipRequest : Entity
{
    /// <summary>How long a request waits for an answer.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    public Guid MembershipRequestKey { get; set; } = Guid.NewGuid();
    public MembershipRequestKind Kind { get; set; }

    /// <summary>The person. A key, not a relationship: they live in another module.</summary>
    public Guid MemberKey { get; set; }

    /// <summary>The kurin the person stands in, whose Звʼязковий answers.</summary>
    public Guid FromKurinKey { get; set; }

    /// <summary>The kurin that asks.</summary>
    public Guid ToKurinKey { get; set; }

    /// <summary>How the person will stand in the asking kurin: юнак or впорядник.</summary>
    public MembershipKind MembershipKind { get; set; } = MembershipKind.Youth;

    public MembershipRequestStatus Status { get; set; } = MembershipRequestStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserKey { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserKey { get; set; }

    /// <summary>
    /// Changes with every answer, so two answers given at once cannot both be saved: the second
    /// finds the row changed and is refused.
    /// </summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public bool IsOpen(DateTime nowUtc) => Status == MembershipRequestStatus.Pending && nowUtc < ExpiresAtUtc;

    /// <summary>Closes the request: approved, declined or withdrawn.</summary>
    public void Close(MembershipRequestStatus status, DateTime nowUtc, Guid? byUserKey)
    {
        Status = status;
        DecidedAtUtc = nowUtc;
        DecidedByUserKey = byUserKey;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>Lets a request lapse now: the person no longer stands in the kurin it asks.</summary>
    public void Lapse(DateTime nowUtc)
    {
        ExpiresAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
