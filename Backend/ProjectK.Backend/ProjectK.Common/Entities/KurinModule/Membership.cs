using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// One person's place in one kurin, for as long as it lasts. A person may hold several at once —
/// юнацьке членство in one kurin and виховництво in another — and closing one leaves everything
/// they earned untouched, because the history belongs to them and not to this row.
/// <para>
/// It belongs to the kurin, not to the person: "this one is mine" is a statement a kurin makes.
/// That is also what lets authorization work without ever reading the member table —
/// <see cref="UserKey"/> is kept here so the scope of a request is answerable from membership alone.
/// </para>
/// </summary>
public class Membership : Entity
{
    public Guid MembershipKey { get; set; } = Guid.NewGuid();

    /// <summary>The person. A key, not a relationship: they live in another module.</summary>
    public Guid MemberKey { get; set; }

    /// <summary>
    /// The account that person signs in with, copied from their record when the account is linked.
    /// Duplicated on purpose: authorization resolves a request's scope from here, and a join to
    /// the member table would put the person back in the middle of every access decision.
    /// </summary>
    public Guid? UserKey { get; set; }

    public Guid KurinKey { get; set; }
    public Guid? GroupKey { get; set; }
    public MembershipKind Kind { get; set; } = MembershipKind.Youth;

    public DateTime JoinedAtUtc { get; set; }

    /// <summary>When they left. Null means the membership is current.</summary>
    public DateTime? LeftAtUtc { get; set; }

    public Kurin Kurin { get; set; } = null!;
    public Group? Group { get; set; }
}
