namespace ProjectK.Common.Models.Records;

/// <summary>
/// Whether a member exists and which account it is linked to. The account flow needs both before it
/// writes anything, and neither is worth loading the member's whole graph for.
/// </summary>
public sealed record MemberAccountLink(Guid MemberKey, Guid? UserKey);

/// <summary>
/// An account opened for a member, and whether the letter inviting them to it actually went out.
/// The account stands either way; a letter that did not go can be sent again from the member.
/// </summary>
public sealed record MemberInvitation(Guid UserKey, bool Sent);

/// <summary>
/// What the profile write leaves behind for the steps that run after it: the member it wrote, whether
/// it was newly created, and the photo it replaced — the callers of those steps have no other way to
/// know, because each runs as its own use case. <paramref name="AccountEmailToFollow"/> says the
/// address changed on a member whose account has to move with it. <paramref name="GroupKey"/> is the
/// гурток they stand in once written, which the response is masked against.
/// </summary>
public sealed record MemberProfileWriteResult(
    Guid MemberKey,
    bool IsCreated,
    bool WasProfileVerifiedCurrent,
    string? PreviousPhotoBlobName,
    bool AccountEmailToFollow = false,
    Guid? GroupKey = null);
