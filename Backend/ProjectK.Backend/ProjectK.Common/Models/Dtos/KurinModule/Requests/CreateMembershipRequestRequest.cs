using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>Body of <c>POST api/kurin/{kurinKey}/membership-requests</c>.</summary>
public sealed class CreateMembershipRequestRequest
{
    /// <summary>The code the person handed over.</summary>
    public string? PublicId { get; set; }

    /// <summary>Instead of a code: someone who once stood in this kurin.</summary>
    public Guid? MemberKey { get; set; }

    public MembershipRequestKind Kind { get; set; } = MembershipRequestKind.Transfer;
    public MembershipKind MembershipKind { get; set; } = MembershipKind.Youth;

    /// <summary>The kurin to ask, when the person stands in several.</summary>
    public Guid? FromKurinKey { get; set; }
}
