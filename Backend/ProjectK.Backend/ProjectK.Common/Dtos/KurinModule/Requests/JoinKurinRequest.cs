using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>
/// Body of <c>POST api/kurin/{kurinKey}/memberships</c>. Name the person by key when they are already
/// on a screen in this kurin, or by their public code when they are not.
/// </summary>
public sealed class JoinKurinRequest
{
    public Guid MemberKey { get; set; }
    public string? PublicId { get; set; }
    public Guid? GroupKey { get; set; }
    public MembershipKind Kind { get; set; } = MembershipKind.Youth;
}
